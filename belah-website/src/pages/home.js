import React from 'react';
import FeaturedProducts from '../components/FeaturedProducts';
import { Link } from 'react-router-dom';
import './home.css';

const Home = () => {
  return (
    <div className="home">
      <section className="home-hero">
        <p className="home-eyebrow">BELAH · FRAGRANCE & MORE</p>
        <h1>Find something worth keeping close.</h1>
        <p>Explore fragrances, accessories, beauty, and new finds from the Belah collection.</p>
        <Link to="/products" className="home-cta">Shop the collection</Link>
      </section>
      <FeaturedProducts />
    </div>
  );
};

export default Home;
