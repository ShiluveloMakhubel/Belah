import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { FaShoppingCart, FaUserCircle, FaBars, FaTimes } from 'react-icons/fa';
import './Header.css';

const Header = () => {
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <header className="header">
      <div className="header-container">
        <div className="logo">
          <Link to="/" onClick={() => setMenuOpen(false)}>
            <img src="/images/logo2.ico" alt="Belah Logo" className="logo-image" />
          </Link>
        </div>

        <nav className={`nav ${menuOpen ? 'open' : ''}`}>
          <ul className="nav-list">
            <li><Link to="/" className="nav-link" onClick={() => setMenuOpen(false)}>Home</Link></li>
            <li><Link to="/products" className="nav-link" onClick={() => setMenuOpen(false)}>Products</Link></li>
            <li><Link to="/about" className="nav-link" onClick={() => setMenuOpen(false)}>About</Link></li>
            <li><Link to="/contact" className="nav-link" onClick={() => setMenuOpen(false)}>Contact</Link></li>
          </ul>
        </nav>

        <div className="header-actions">
          <div className="search-container">
            <input
              type="search"
              placeholder="Search products..."
              aria-label="Search products"
              className="search-input"
            />
            <button className="search-button" aria-label="Search">
              🔍
            </button>
          </div>

          <Link to="/cart" className="icon-button cart-button" aria-label="View Cart">
            <FaShoppingCart />
            <span className="cart-count">3</span> {/* Replace 3 with dynamic cart count */}
          </Link>

          <Link to="/login" className="icon-button user-button" aria-label="User Account">
            <FaUserCircle />
          </Link>

          <button
            className="menu-toggle"
            onClick={() => setMenuOpen(!menuOpen)}
            aria-label={menuOpen ? 'Close menu' : 'Open menu'}
          >
            {menuOpen ? <FaTimes /> : <FaBars />}
          </button>
        </div>
      </div>
    </header>
  );
};

export default Header;
