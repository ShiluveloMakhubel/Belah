import React, { useEffect, useState } from "react";
import { PaystackButton } from "react-paystack";
import "./CheckoutPage.css"; // Ensure you have a CSS file for styling

const CheckOutPage = () => {
  const [cart, setCart] = useState([]);
  const [currentStep, setCurrentStep] = useState(2);
  const [userDetails, setUserDetails] = useState({
    name: "",
    email: "",
    phone: "",
    address: "",
    city: "",
  });

  const publicKey = "pk_test_25ffef826e29ce24e95c5bf55fff271f7b0fcf54"; // Replace with your Paystack public key

  // Load cart from localStorage
  useEffect(() => {
    const savedCart = JSON.parse(localStorage.getItem("cart")) || [];
    setCart(savedCart);
  }, []);

  const totalAmount = cart.reduce(
    (total, item) => total + item.price * item.quantity,
    0
  );
  const amountInKobo = totalAmount * 100; // Paystack expects kobo for NGN

  const handleChange = (e) => {
    setUserDetails({ ...userDetails, [e.target.name]: e.target.value });
  };

  const componentProps = {
    email: userDetails.email,
    amount: amountInKobo,
    currency: "ZAR", // Use "NGN" for testing; enable ZAR in Paystack for South Africa
    metadata: {
      name: userDetails.name,
      phone: userDetails.phone,
      address: userDetails.address,
      city: userDetails.city,
      cart_items: cart.map((item) => `${item.name} x${item.quantity}`).join(", "),
    },
    publicKey,
    text: "Pay Now",
    onSuccess: (response) => {
      alert("Payment successful! 🎉 Reference: " + response.reference);
      localStorage.removeItem("cart");
      // Optionally save order details to your backend
    },
    onClose: () => alert("Transaction was not completed, window closed."),
  };

  if (cart.length === 0) {
    return <h2>Your cart is empty</h2>;
  }

  return (
    <div className="checkout-page">
      <h2>Checkout</h2>
         {/* 🔹 Step Indicator */}
      <div className="step-indicator">
        <div className={`step ${currentStep >= 1 ? 'active' : ''}`}>Cart</div>
        <div className={`step ${currentStep >= 2 ? 'active' : ''}`}>Delivery</div>
        <div className={`step ${currentStep >= 3 ? 'active' : ''}`}>Payment</div>
      </div>
      <div className="cart-summary">
        <h3>Order Summary</h3>
        <ul>
          {cart.map((item, index) => (
            <li key={index}>
              {item.name} x{item.quantity} - ${item.price * item.quantity}
            </li>
          ))}
        </ul>
        <h3>Total: ${totalAmount}</h3>
      </div>

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
          placeholder="Email"
          value={userDetails.email}
          onChange={handleChange}
          required
        />
        <input
          type="text"
          name="phone"
          placeholder="Phone Number"
          value={userDetails.phone}
          onChange={handleChange}
          required
        />
        <input
          type="text"
          name="address"
          placeholder="Delivery Address"
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
      </div>

      <PaystackButton className="paystack-button" {...componentProps} />
    </div>
  );
};

export default CheckOutPage;
