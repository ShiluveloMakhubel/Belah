import React, { useEffect, useState } from 'react';
import './Categories.css';
import { Link } from 'react-router-dom';
import API_BASE_URL from '../config/api';

const Categories = () => {
  const [categories, setCategories] = useState([]);
  const [error, setError] = useState('');
  useEffect(() => {
    fetch(`${API_BASE_URL}/api/categories`)
      .then((response) => {
        if (!response.ok) throw new Error('Could not load categories.');
        return response.json();
      })
      .then((data) => setCategories(data.filter((category) => category.isActive)))
      .catch(() => setError('Categories are temporarily unavailable.'));
  }, []);

  return (
    <div className="categories-container">
      <h2 className="categories-title">Shop by Category</h2>
      {error && <p role="alert">{error}</p>}
      {!error && categories.length === 0 && <p>Categories will appear here when available.</p>}
      <div className="category-grid">
        {categories.map((category) => (
          <Link to={`/products?category=${encodeURIComponent(category.name)}`} key={category.id} className="category-card">
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
