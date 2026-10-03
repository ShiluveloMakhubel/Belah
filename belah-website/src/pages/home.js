import React from 'react';
import FeaturedProducts from '../components/FeaturedProducts';
import Categories from '../components/Categories';
import PromotionalBanner from '../components/PromotionalBanner';
import Deals from '../components/Deals'; 
import './home.css';

const Home = () => {
  return (
    <div className="home">
    
      <Deals />
      <FeaturedProducts />
    </div>
  );
};

export default Home;
