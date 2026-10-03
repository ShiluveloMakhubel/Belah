import React from 'react';
import './Footer.css';
import { Link } from 'react-router-dom';

const Footer = () => {
  return (
    <footer className="footer">
      <div className="footer-gradient">
        <div className="footer-container">
          <div className="footer-section">
            <h4>Contact</h4>
            <p>Email: <a href="mailto:moyanethembelani@gmail.com">moyanethembelani@gmail.com</a></p>
            <p>Phone 1: <a href="tel:+27671707279">(+27) 67-170-7279</a></p>
            <p>Phone 2: <a href="tel:+27633245982">(+27) 63-324-5982</a></p>
          </div>

          <div className="footer-section">
            <h4>Follow</h4>
            <div className="social-icons">
              <a href="https://facebook.com/thembelanimoyane" target="_blank" rel="noopener noreferrer">Facebook</a>
              <a href="https://instagram.com/thembelanimoyane" target="_blank" rel="noopener noreferrer">Instagram</a>
              <a href="https://tiktok.com/moyanebelah" target="_blank" rel="noopener noreferrer">TikTok</a>
            </div>
          </div>

          <div className="footer-section">
            <h4>Company</h4>
            <p><Link to="/about">About Us</Link></p>
            <p><Link to="/contact">Contact Page</Link></p>
            <p><Link to="/products">Shop Now</Link></p>
          </div>
        </div>

        <div className="footer-bottom">
          <p>© {new Date().getFullYear()} Belah. All rights reserved.</p>
        </div>
      </div>
    </footer>
  );
};

export default Footer;
