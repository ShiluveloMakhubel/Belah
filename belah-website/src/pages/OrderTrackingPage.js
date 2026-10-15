import React, { useState, useEffect } from 'react';
import { API_BASE_URL } from '../config/api';
import './OrderTrackingPage.css';

const OrderTrackingPage = () => {
  const [orderNumber, setOrderNumber] = useState('');
  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const requestedOrder = new URLSearchParams(window.location.search).get('orderNumber');
    if (!requestedOrder) return;
    setOrderNumber(requestedOrder);
    setLoading(true);
    fetch(`${API_BASE_URL}/api/orders/${encodeURIComponent(requestedOrder)}`)
      .then(async (response) => {
        if (!response.ok) throw new Error(response.status === 404 ? 'Order not found. Please check your order number.' : 'Unable to retrieve order details.');
        setOrder(await response.json());
      })
      .catch((err) => setError(err.message || 'Error looking up order.'))
      .finally(() => setLoading(false));
  }, []);

  const handleTrack = async (e) => {
    e.preventDefault();
    if (!orderNumber.trim()) {
      setError('Please enter your order number');
      return;
    }

    setLoading(true);
    setError('');
    setOrder(null);

    try {
      const res = await fetch(`${API_BASE_URL}/api/orders/${encodeURIComponent(orderNumber.trim())}`);
      if (!res.ok) {
        if (res.status === 404) {
          throw new Error('Order not found. Please check your order number.');
        }
        throw new Error('Unable to retrieve order details.');
      }

      const data = await res.json();
      setOrder(data);
    } catch (err) {
      setError(err.message || 'Error looking up order.');
    } finally {
      setLoading(false);
    }
  };

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'delivered') return 'status-completed';
    if (s === 'shipped') return 'status-shipped';
    if (s === 'processing') return 'status-processing';
    if (s === 'cancelled') return 'status-cancelled';
    return 'status-pending';
  };

  return (
    <div className="tracking-container">
      <div className="tracking-card">
        <h2>Track Your Belah Order</h2>
        <p className="tracking-subtitle">
          Enter your unique order number (e.g. <code>BELAH-20261010-XXXXXX</code>) to view fulfillment and payment status.
        </p>

        <form onSubmit={handleTrack} className="tracking-form">
          <input
            type="text"
            placeholder="Order Number"
            value={orderNumber}
            onChange={(e) => setOrderNumber(e.target.value)}
            disabled={loading}
            required
          />
          <button type="submit" disabled={loading}>
            {loading ? 'Searching...' : 'Track Order'}
          </button>
        </form>

        {error && <div className="tracking-error">⚠️ {error}</div>}

        {order && (
          <div className="tracking-result">
            <div className="result-header">
              <div>
                <span className="order-label">Order Reference:</span>
                <h3>{order.orderNumber}</h3>
                <span className="order-date">
                  Placed on {new Date(order.orderDate).toLocaleDateString()} at{' '}
                  {new Date(order.orderDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </span>
              </div>
              <div className="status-badges">
                <span className={`badge ${getStatusBadge(order.status)}`}>
                  Status: {order.status}
                </span>
                <span className={`badge payment-${(order.paymentStatus || 'pending').toLowerCase()}`}>
                  Payment: {order.paymentStatus}
                </span>
              </div>
            </div>

            <div className="items-summary">
              <h4>Items in Order</h4>
              <ul>
                {order.items && order.items.map((item, idx) => (
                  <li key={idx}>
                    <div className="item-name">
                      <strong>{item.productName}</strong>
                      {(item.selectedVariantName || item.selectedColor || item.selectedStorage) && (
                        <span className="item-variant">
                          ({[item.selectedVariantName, item.selectedColor, item.selectedStorage].filter(Boolean).join(' / ')})
                        </span>
                      )}
                    </div>
                    <div className="item-qty-price">
                      {item.quantity} × R{item.unitPrice.toFixed(2)} = <strong>R{item.subtotal.toFixed(2)}</strong>
                    </div>
                  </li>
                ))}
              </ul>
              {Number(order.deliveryFee) > 0 && <div className="order-total-row"><span>Delivery fee:</span><strong>R{Number(order.deliveryFee).toFixed(2)}</strong></div>}
              <div className="order-total-row">
                <span>Total Amount:</span>
                <strong>R{order.totalAmount.toFixed(2)}</strong>
              </div>
            </div>

            <div className="privacy-note">
              🔒 <em>Personal delivery address and payment credentials are kept private on public tracking.</em>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default OrderTrackingPage;
