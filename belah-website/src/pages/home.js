import React from 'react';
import FeaturedProducts from '../components/FeaturedProducts';
import { Link } from 'react-router-dom';
import './home.css';

const Home = () => {
  return (
    <div className="home">
      <section className="home-hero">
        <p className="home-eyebrow">BELAH · HAIR COLLECTION</p>
        <h1>Find your next favourite look.</h1>
        <p>Explore wigs, frontals, bundles, and more from the Belah collection.</p>
        <Link to="/products" className="home-cta">Shop the collection</Link>
      </section>
      <FeaturedProducts />
    </div>
  );
};

export default Home;
