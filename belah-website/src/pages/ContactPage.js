import React from 'react';
import { FaWhatsapp, FaTiktok, FaFacebook, FaInstagram } from 'react-icons/fa';
import './ContactPage.css';

const ContactPage = () => {
  return (
    <div className="contact-page">
      <div className="contact-container">
        <h2>Contact Us</h2>
        <div className="contact-details">
          <h3>WhatsApp</h3>
          <p>
            <a href="https://wa.me/27671707279" target="_blank" rel="noopener noreferrer">
              <FaWhatsapp className="contact-icon" /> +27671707279
            </a>
          </p>
          <p>
            <a href="https://wa.me/27633245982" target="_blank" rel="noopener noreferrer">
              <FaWhatsapp className="contact-icon" /> +27633245982
            </a>
          </p>

          <h3>Social Media</h3>
          <p>
            <a href="https://www.tiktok.com/@your_tiktok_username" target="_blank" rel="noopener noreferrer">
              <FaTiktok className="contact-icon" /> TikTok
            </a>
          </p>
          <p>
            <a href="https://www.facebook.com/your_facebook_username" target="_blank" rel="noopener noreferrer">
              <FaFacebook className="contact-icon" /> Facebook
            </a>
          </p>
          <p>
            <a href="https://www.instagram.com/your_instagram_username" target="_blank" rel="noopener noreferrer">
              <FaInstagram className="contact-icon" /> Instagram
            </a>
          </p>

          <h3>Address</h3>
          <p>Blue Lakes Estates, Kleinfontein, Benoni</p>
        </div>
      </div>
    </div>
  );
};

export default ContactPage;
