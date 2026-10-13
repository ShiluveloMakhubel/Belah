import React, { useEffect, useState, useRef } from "react";
import { Link } from "react-router-dom";
import { API_BASE_URL } from "../config/api";
import "./CheckoutPage.css";

const readApiResponse = async (response) => {
  try {
    return await response.json();
  } catch {
    // Reverse proxies and unhandled server errors can return HTML or an empty body.
    // Keep those implementation details out of the customer-facing checkout message.
    return {};
  }
};

const CheckOutPage = () => {
  const createOrderInFlight = useRef(false);
  const [cart, setCart] = useState([]);
  const [currentStep, setCurrentStep] = useState(2);
  const [pendingOrder, setPendingOrder] = useState(null);
  const [orderError, setOrderError] = useState(null);
  const [isCreatingOrder, setIsCreatingOrder] = useState(false);
  const [isVerifyingPayment, setIsVerifyingPayment] = useState(false);
  const [confirmedOrder, setConfirmedOrder] = useState(null);
  const [authorizationUrl, setAuthorizationUrl] = useState(null);
  const [paymentReference, setPaymentReference] = useState('');
  const [isPaymentReturn, setIsPaymentReturn] = useState(() => {
    const params = new URLSearchParams(window.location.search);
    return params.has("reference") || params.has("trxref");
  });

  const [userDetails, setUserDetails] = useState({
    name: "",
    email: "",
    phone: "",
    address: "",
    city: "",
    province: "",
    postalCode: "",
  });

  // Load cart from localStorage
  useEffect(() => {
    const savedCart = JSON.parse(localStorage.getItem("cart")) || [];
    setCart(savedCart);
  }, []);

  useEffect(() => {
    const query = new URLSearchParams(window.location.search);
    const reference = query.get("reference") || query.get("trxref");
    if (!reference) return;
    const capability = sessionStorage.getItem(`belah_order_capability_${reference}`);
    const token = localStorage.getItem("belah_token");
    setIsVerifyingPayment(true);
    fetch(`${API_BASE_URL}/api/payments/verify`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({ reference, guestCapability: capability || null }),
    })
      .then(async (response) => {
        const data = await readApiResponse(response);
        if (!response.ok) {
          const message = response.status >= 500
            ? "Belah could not verify the payment because of a server error. Please retry verification shortly or contact support."
            : data?.message || "Payment verification failed.";
          const failure = new Error(message);
          failure.status = response.status;
          throw failure;
        }
        localStorage.removeItem("cart");
        sessionStorage.removeItem(`belah_order_capability_${reference}`);
        setCart([]);
        setConfirmedOrder(data);
        setCurrentStep(4);
        setIsPaymentReturn(false);
        window.history.replaceState({}, document.title, window.location.pathname);
      })
      .catch((error) => {
        setPaymentReference(reference);
        const manualRefund = error.message?.toLowerCase().includes('refund');
        setOrderError(`${error.message || 'Could not verify payment.'}${manualRefund ? ' Do not pay again; the Belah team must review this order.' : error.status === 409 ? ' If the payment is still pending, retry verification shortly. Do not start another checkout for this order.' : ''}`);
      })
      .finally(() => setIsVerifyingPayment(false));
  }, []);

  const totalAmount = cart.reduce(
    (total, item) => total + item.price * item.quantity,
    0
  );
  const handleChange = (e) => {
    setUserDetails({ ...userDetails, [e.target.name]: e.target.value });
  };

  // Step 1: Create a pending order on the backend with server-side pricing & stock validation
  const createPendingOrder = async () => {
    if (createOrderInFlight.current) return null;
    createOrderInFlight.current = true;
    setOrderError(null);
    setIsCreatingOrder(true);

    const orderPayload = {
      customerName: userDetails.name.trim(),
      email: userDetails.email.trim(),
      phone: userDetails.phone.trim(),
      address: userDetails.address.trim(),
      city: userDetails.city.trim(),
      province: userDetails.province.trim(),
      postalCode: userDetails.postalCode.trim(),
      items: cart.map((item) => ({
        productId: item.id,
        quantity: item.quantity,
        selectedColor: item.selectedColor || null,
        selectedStorage: item.selectedStorage || null,
      })),
    };

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(orderPayload.email) || orderPayload.phone.length < 7 || orderPayload.items.length === 0 || orderPayload.items.some((item) => !Number.isInteger(item.quantity) || item.quantity < 1)) {
      setOrderError('Enter a valid email and phone number, and make sure your cart contains valid quantities.');
      setIsCreatingOrder(false);
      createOrderInFlight.current = false;
      return null;
    }

    let createdOrderNumber = null;
    try {
      // 1. Create order
      const response = await fetch(`${API_BASE_URL}/api/orders`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(localStorage.getItem("belah_token") ? { Authorization: `Bearer ${localStorage.getItem("belah_token")}` } : {}),
        },
        body: JSON.stringify(orderPayload),
      });

      const data = await readApiResponse(response);

      if (!response.ok) {
        const message = response.status >= 500
          ? "Belah could not save your order because of a server error. No payment was started. Please try again later or contact support."
          : data?.message || data?.title || "Belah could not create your order. Check your details and try again.";
        setOrderError(message);
        setIsCreatingOrder(false);
        createOrderInFlight.current = false;
        return null;
      }
      createdOrderNumber = data.orderNumber || null;

      // 2. Initialize payment session on backend to associate reference with order
      const initRes = await fetch(`${API_BASE_URL}/api/payments/initialize`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(localStorage.getItem("belah_token") ? { Authorization: `Bearer ${localStorage.getItem("belah_token")}` } : {}),
        },
        body: JSON.stringify({ orderNumber: data.orderNumber, guestCapability: data.guestCapability || null }),
      });

      const initData = await readApiResponse(initRes);
      if (!initRes.ok) {
        const reason = initRes.status >= 500
          ? "a server error prevented checkout setup"
          : initData?.message || "checkout setup was rejected";
        throw new Error(`Order ${data.orderNumber} was saved, but payment could not be set up: ${reason}. No payment was completed. Contact Belah before retrying.`);
      }
      if (!initData.authorizationUrl) {
        throw new Error(`Order ${data.orderNumber} was saved, but secure checkout could not be confirmed. No payment was completed. Contact Belah before retrying.`);
      }
      if (data.guestCapability) sessionStorage.setItem(`belah_order_capability_${initData.paymentReference}`, data.guestCapability);
      data.paymentReference = initData.paymentReference;
      data.totalAmount = initData.amount;
      setAuthorizationUrl(initData.authorizationUrl);
      setPaymentReference(initData.paymentReference);

      setPendingOrder(data);
      setCurrentStep(3);
      setIsCreatingOrder(false);
      createOrderInFlight.current = false;
      return data;
    } catch (err) {
      const message = createdOrderNumber
        ? err.message?.startsWith(`Order ${createdOrderNumber} was saved`)
          ? err.message
          : `Order ${createdOrderNumber} was saved, but secure checkout setup could not be confirmed. No payment was completed. Contact Belah before retrying.`
        : err?.name === "TypeError"
          ? "Belah could not be reached to create your order. No payment was started. Check your connection and try again."
          : err.message || "Belah could not create your order. Please try again or contact support.";
      setOrderError(message);
      setIsCreatingOrder(false);
      createOrderInFlight.current = false;
      return null;
    }
  };

  const isFormComplete =
    userDetails.name.trim() &&
    userDetails.email.trim() &&
    userDetails.phone.trim() &&
    userDetails.address.trim() &&
    userDetails.city.trim() &&
    userDetails.province.trim() &&
    userDetails.postalCode.trim();

  // Completed Step 4
  if (currentStep === 4 && confirmedOrder) {
    return (
      <div className="checkout-page">
        <div className="confirmation-card" style={{ textAlign: "center", padding: "40px 20px" }}>
          <div style={{ fontSize: "3rem", marginBottom: "16px" }}>🎉</div>
          <h2 style={{ color: "#065f46", marginBottom: "8px" }}>Order Confirmed & Paid!</h2>
          <p style={{ color: "#374151", fontSize: "1.1rem" }}>
            Thank you for shopping with Belah Hair. Your payment has been verified directly by our server.
          </p>
          <div style={{ background: "#f9fafb", padding: "20px", borderRadius: "8px", margin: "24px auto", maxWidth: "450px" }}>
            <p><strong>Order Number:</strong> {confirmedOrder.orderNumber}</p>
            <p><strong>Payment Status:</strong> <span style={{ color: "#065f46", fontWeight: "bold" }}>{confirmedOrder.paymentStatus}</span></p>
            <p><strong>Fulfillment:</strong> {confirmedOrder.orderStatus}</p>
            <p><strong>Amount:</strong> R{confirmedOrder.amount.toFixed(2)}</p>
          </div>
          <div style={{ display: "flex", gap: "12px", justifyContent: "center" }}>
            <Link to={`/track-order`} className="proceed-to-payment-btn" style={{ textDecoration: "none", display: "inline-block", maxWidth: "200px" }}>
              Track My Order
            </Link>
            <Link to="/products" className="btn-cancel" style={{ textDecoration: "none", display: "inline-block", padding: "14px 20px", borderRadius: "6px" }}>
              Continue Shopping
            </Link>
          </div>
        </div>
      </div>
    );
  }

  if (isPaymentReturn && !confirmedOrder) {
    return (
      <div className="checkout-page" role="status">
        <h2>{isVerifyingPayment ? "Verifying your payment" : "Payment verification needs attention"}</h2>
        <p>{isVerifyingPayment ? "Belah is checking the transaction directly with Paystack." : orderError}</p>
        {!isVerifyingPayment && <div className="payment-recovery"><p>{orderError}</p>{paymentReference && <button type="button" onClick={async () => {
          setIsVerifyingPayment(true); setOrderError(null);
          try {
            const capability = sessionStorage.getItem(`belah_order_capability_${paymentReference}`);
            const response = await fetch(`${API_BASE_URL}/api/payments/verify`, { method: 'POST', headers: { 'Content-Type': 'application/json', ...(localStorage.getItem('belah_token') ? { Authorization: `Bearer ${localStorage.getItem('belah_token')}` } : {}) }, body: JSON.stringify({ reference: paymentReference, guestCapability: capability || null }) });
            const result = await readApiResponse(response);
            if (!response.ok) throw new Error(result?.message || 'Verification is still pending.');
            localStorage.removeItem('cart'); sessionStorage.removeItem(`belah_order_capability_${paymentReference}`); setCart([]); setConfirmedOrder(result); setCurrentStep(4); setIsPaymentReturn(false); window.history.replaceState({}, document.title, window.location.pathname);
          } catch (err) { setOrderError(err.message || 'Verification is still pending. Retry shortly.'); }
          finally { setIsVerifyingPayment(false); }
        }}>Retry payment verification</button>} <Link to="/track-order">Track order status</Link></div>}
      </div>
    );
  }

  if (cart.length === 0 && currentStep !== 4 && !isPaymentReturn) {
    return (
      <div className="checkout-page" style={{ textAlign: "center", padding: "50px 20px" }}>
        <h2>Your cart is empty</h2>
        <p style={{ color: "#6b7280", marginTop: "10px" }}>Discover our luxury hair collection and bundles.</p>
        <Link to="/products" className="proceed-to-payment-btn" style={{ textDecoration: "none", display: "inline-block", maxWidth: "220px", marginTop: "20px" }}>
          Browse Products
        </Link>
      </div>
    );
  }

  return (
    <div className="checkout-page">
      <h2>Checkout</h2>

      {/* Step Indicator */}
      <div className="step-indicator">
        <div className={`step ${currentStep >= 1 ? "active" : ""}`}>Cart</div>
        <div className={`step ${currentStep >= 2 ? "active" : ""}`}>Delivery</div>
        <div className={`step ${currentStep >= 3 ? "active" : ""}`}>Payment</div>
      </div>

      <div className="cart-summary">
        <h3>Order Summary</h3>
        <ul>
          {cart.map((item, index) => (
            <li key={index}>
              {item.name} x{item.quantity} - R{(item.price * item.quantity).toFixed(2)}
            </li>
          ))}
        </ul>
        <h3>Total: R{(pendingOrder?.totalAmount ?? totalAmount).toFixed(2)}</h3>
      </div>

      {/* Delivery Details Form */}
      {currentStep === 2 && (
        <div className="delivery-details">
          <h3>Delivery Details</h3>
          <input
            type="text"
            name="name"
            placeholder="Full Name"
            value={userDetails.name}
            onChange={handleChange}
            required
          />
          <input
            type="email"
            name="email"
            placeholder="Email Address"
            value={userDetails.email}
            onChange={handleChange}
            required
          />
          <input
            type="text"
            name="phone"
            placeholder="Phone Number (e.g. 082 123 4567)"
            value={userDetails.phone}
            onChange={handleChange}
            required
          />
          <input
            type="text"
            name="address"
            placeholder="Delivery Street Address"
            value={userDetails.address}
            onChange={handleChange}
            required
          />
          <input
            type="text"
            name="city"
            placeholder="City"
            value={userDetails.city}
            onChange={handleChange}
            required
          />
          <input
            type="text"
            name="province"
            placeholder="Province (e.g. Gauteng, Western Cape)"
            value={userDetails.province}
            onChange={handleChange}
            required
          />
          <input
            type="text"
            name="postalCode"
            placeholder="Postal Code"
            value={userDetails.postalCode}
            onChange={handleChange}
            required
          />

          {orderError && (
            <div className="order-error" role="alert" aria-live="assertive">
              <p>⚠️ {orderError}</p>
            </div>
          )}

          <button
            className="proceed-to-payment-btn"
            onClick={createPendingOrder}
            disabled={!isFormComplete || isCreatingOrder}
          >
            {isCreatingOrder ? "Creating order..." : "Proceed to Payment"}
          </button>
        </div>
      )}

      {/* Payment Step */}
      {currentStep === 3 && pendingOrder && (
        <div className="payment-step">
          <h3>Payment</h3>
          <p>Order <strong>{pendingOrder.orderNumber}</strong> has been created for R{pendingOrder.totalAmount.toFixed(2)}.</p>

          {orderError && (
            <div className="order-error" role="alert" aria-live="assertive">
              <p>⚠️ {orderError}</p>
            </div>
          )}

          {isVerifyingPayment ? (
            <div style={{ padding: "20px", color: "#1e40af", fontWeight: "bold" }}>Verifying payment with Paystack...</div>
          ) : (
            <button className="paystack-button" disabled={!authorizationUrl} onClick={() => window.location.assign(authorizationUrl)}>
              Continue to secure Paystack checkout
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default CheckOutPage;
