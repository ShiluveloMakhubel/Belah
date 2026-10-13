import React, { useState, useEffect } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { FaShoppingCart, FaUserCircle, FaBars, FaTimes, FaSignOutAlt } from 'react-icons/fa';
import { useAuth } from '../config/AuthContext';
import './Header.css';

const Header = () => {
  const [menuOpen, setMenuOpen] = useState(false);
  const [cartCount, setCartCount] = useState(0);
  const { user, isAdmin, logout } = useAuth();
  const navigate = useNavigate();

  // Dynamically calculate cart count from localStorage
  const updateCartCount = () => {
    try {
      const cart = JSON.parse(localStorage.getItem('cart')) || [];
      const count = cart.reduce((total, item) => total + (item.quantity || 1), 0);
      setCartCount(count);
    } catch {
      setCartCount(0);
    }
  };

  useEffect(() => {
    updateCartCount();
    window.addEventListener('storage', updateCartCount);
    const interval = setInterval(updateCartCount, 1500);
    return () => {
      window.removeEventListener('storage', updateCartCount);
      clearInterval(interval);
    };
  }, []);

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <header className="header">
      <div className="header-container">
        <div className="logo">
          <Link to="/" onClick={() => setMenuOpen(false)}>
            <span className="logo-wordmark">BELAH</span>
            <span className="logo-tagline">HAIR · BEAUTY</span>
          </Link>
        </div>

        <nav id="primary-navigation" className={`nav ${menuOpen ? 'open' : ''}`} aria-label="Main navigation">
          <ul className="nav-list">
            <li><NavLink to="/" end className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setMenuOpen(false)}>Home</NavLink></li>
            <li><NavLink to="/products" className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setMenuOpen(false)}>Shop</NavLink></li>
            <li><NavLink to="/track-order" className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setMenuOpen(false)}>Track Order</NavLink></li>
            {user && !isAdmin && <li><NavLink to="/my-account" className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setMenuOpen(false)}>My Account</NavLink></li>}
            <li><NavLink to="/about" className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setMenuOpen(false)}>About</NavLink></li>
            <li><NavLink to="/contact" className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setMenuOpen(false)}>Contact</NavLink></li>
            {isAdmin && (
              <li>
                <NavLink to="/admin" className={({ isActive }) => `nav-link admin-nav-link${isActive ? ' active' : ''}`} onClick={() => setMenuOpen(false)}>Admin</NavLink>
              </li>
            )}
          </ul>
        </nav>

        <div className="header-actions">
            <Link to="/cart" className="icon-button cart-button" aria-label={`View cart, ${cartCount} items`}>
            <FaShoppingCart />
              <span className="cart-label">Cart</span>
              {cartCount > 0 && <span className="cart-count" aria-hidden="true">{cartCount}</span>}
          </Link>

          {user ? (
            <div className="user-logged-in-menu">
              <Link to={isAdmin ? "/admin" : "/my-account"} className="user-greeting">
                <span>{user.username}</span>
              </Link>
              <button onClick={handleLogout} className="icon-button logout-button" title="Sign out" aria-label="Sign out">
                <FaSignOutAlt />
              </button>
            </div>
          ) : (
            <Link to="/login" className="icon-button user-button" aria-label="User Account" title="Sign In">
              <FaUserCircle />
            </Link>
          )}

          <button
            className="menu-toggle"
            onClick={() => setMenuOpen(!menuOpen)}
            aria-label={menuOpen ? 'Close menu' : 'Open menu'}
            aria-expanded={menuOpen}
            aria-controls="primary-navigation"
          >
            {menuOpen ? <FaTimes /> : <FaBars />}
          </button>
        </div>
      </div>
    </header>
  );
};

export default Header;
