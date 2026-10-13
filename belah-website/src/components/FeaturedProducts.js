import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { API_BASE_URL, resolveProductImage } from '../config/api';
import './FeaturedProducts.css';

const FeaturedProducts = () => {
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchProducts = async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/api/products`);
        if (!res.ok) return;
        const data = await res.json();
        setProducts(data.slice(0, 4));
      } catch (error) {
        console.error('Error fetching products:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchProducts();
  }, []);

  return (
    <div className="featured-products">
      <h2>Featured Products</h2>
      {loading && <p role="status">Loading the collection…</p>}
      {!loading && products.length === 0 && <p>The collection is currently unavailable. Please try again later.</p>}
      <div className="product-list">
        {products.map(product => (
          <Link key={product.id} to={`/product/${product.id}`} className="product-item" style={{ textDecoration: 'none', color: 'inherit' }}>
            <img
              src={resolveProductImage(product.imageUrl)}
              alt={product.name}
              onError={(e) => { e.currentTarget.onerror = null; e.currentTarget.src = '/images/product-placeholder.svg'; }}
            />
            <h3>{product.name}</h3>
            <p style={{ fontWeight: '600', color: '#c59b27' }}>R{product.price ? product.price.toFixed(2) : '0.00'}</p>
          </Link>
        ))}
      </div>
    </div>
  );
};

export default FeaturedProducts;
