import React from 'react';
import './Categories.css';

import { Link } from 'react-router-dom'; // for navigation

const categoriesData = [
  { id: 1, name: 'Hair Weave', image: '/images/hair2.png', slug: 'hair-weave' },
  { id: 2, name: 'Smartphone', image: '/images/iphone11.png', slug: 'smartphone' },
  { id: 3, name: 'Shoes', image: '/images/sandals.jpg', slug: 'shoes' },
];

const Categories = () => {
  return (
    <div className="categories-container">
      
      <h2 className="categories-title">Shop by Category</h2>
      <div className="category-grid">
        {categoriesData.map((category) => (
          <Link to={`/products/${category.slug}`} key={category.id} className="category-card">
            <img src={category.image} alt={category.name} className="category-image" />
            <div className="category-info">
              <h3>{category.name}</h3>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
};

export default Categories;
