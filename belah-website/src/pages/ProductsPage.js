// ProductsPage.js
import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import './ProductPage.css';

const ProductsPage = () => {
  const [products, setProducts] = useState([]);

  useEffect(() => {
    const fetchProducts = async () => {
      try {
        const res = await fetch('http://localhost:5000/api/products');
        const data = await res.json();

        // Filter for iPhones only (case-insensitive check)
        const iphoneProducts = data.filter(
          (product) =>
            product.category?.toLowerCase() === 'phone' &&
            product.name?.toLowerCase().includes('iphone')
        );

        setProducts(iphoneProducts);
      } catch (err) {
        console.error('Failed to fetch products:', err);
      }
    };

    fetchProducts();
  }, []);

  return (
    <div className="products-page">
      <h2>iPhones</h2>

      <div className="product-list">
        {products.length > 0 ? (
          products.map((product) => (
            <div key={product.id} className="product-item">
              <Link to={`/product/${product.id}`}>
                <img
                  src={product.imageUrl}
                  alt={product.name}
                  className="product-thumbnail"
                />
              </Link>
              <h3>{product.name}</h3>
              <p>Price: {product.price}</p>
            </div>
          ))
        ) : (
          <p>No iPhones available at the moment.</p>
        )}
      </div>
    </div>
  );
};

export default ProductsPage;
