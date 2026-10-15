import React, { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../config/AuthContext';
import apiClient from '../config/apiClient';
import './MyAccountPage.css';

const MyAccountPage = () => {
  const { user, loading: authLoading, logout } = useAuth();
  const navigate = useNavigate();
  const [orders, setOrders] = useState([]);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (authLoading) return;
    if (!user) {
      setLoading(false);
      setError('Sign in to view your account and order history.');
      return;
    }
    let active = true;
    Promise.all([apiClient.get('/api/User/me'), apiClient.get('/api/orders/my-orders')])
      .then(([profileResponse, ordersResponse]) => { if (active) { setProfile(profileResponse.data); setOrders(ordersResponse.data); } })
      .catch((err) => {
        if (!active) return;
        if (err.response?.status === 401) {
          logout();
          setError('Your session expired. Sign in again to view your order history.');
        } else if (err.response?.status === 403) setError('Your account cannot access this order history.');
        else setError('We could not load your orders. Check your connection and retry.');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [authLoading, user, logout]);

  if (authLoading || loading) return <main className="account-page"><p role="status">Loading your account…</p></main>;
  if (!user) return <main className="account-page"><section className="account-card"><h1>My account</h1><p>{error}</p><Link to="/login">Sign in</Link></section></main>;

  return <main className="account-page">
    <section className="account-card">
      <h1>My account</h1>
      <h2>Profile</h2>
      <dl className="account-profile"><dt>Username</dt><dd>{profile?.username || user.username}</dd><dt>Email</dt><dd>{profile?.email || '—'}</dd><dt>Account type</dt><dd>{profile?.role === 'Admin' || user.role === 'Admin' ? 'Administrator' : 'Customer'}</dd>{profile?.createdAt && <><dt>Member since</dt><dd>{new Date(profile.createdAt).toLocaleDateString()}</dd></>}</dl>
    </section>
    <section className="account-card">
      <h2>Your orders</h2>
      {error && <div role="alert" className="account-error">{error} <button type="button" onClick={() => navigate(0)}>Retry</button></div>}
      {!error && orders.length === 0 && <p>You have not placed an order yet. <Link to="/products">Explore the collection</Link>.</p>}
      {orders.map((order) => <article className="account-order" key={order.orderId}>
        <div className="account-order-heading"><div><h3>{order.orderNumber}</h3><time dateTime={order.orderDate}>{new Date(order.orderDate).toLocaleString()}</time></div><strong>R{Number(order.totalAmount).toFixed(2)}</strong></div>
        <p>Payment: <strong>{order.paymentStatus}</strong> · Fulfilment: <strong>{order.status}</strong></p>
        {Number(order.deliveryFee) > 0 && <p>Delivery fee: R{Number(order.deliveryFee).toFixed(2)}</p>}
        <ul>{order.items?.map((item) => <li key={item.id}>{item.quantity} × {item.productName} — R{Number(item.subtotal).toFixed(2)}{(item.selectedVariantName || item.selectedColor || item.selectedStorage) ? ` (${[item.selectedVariantName, item.selectedColor, item.selectedStorage].filter(Boolean).join(' / ')})` : ''}</li>)}</ul>
        <Link to={`/track-order?orderNumber=${encodeURIComponent(order.orderNumber)}`}>Track this order</Link>
      </article>)}
    </section>
  </main>;
};

export default MyAccountPage;
