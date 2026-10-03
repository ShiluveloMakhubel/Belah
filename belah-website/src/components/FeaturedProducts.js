import React, { useEffect, useState } from 'react';
import './FeaturedProducts.css';

const FeaturedProducts = () => {
  const [products, setProducts] = useState([]);

  useEffect(() => {
    const fetchProducts = async () => {
      try {
        const res = await fetch('http://localhost:5000/api/products');
        const data = await res.json();

        // Optional: Only show first 8 or add filtering logic if needed
        setProducts(data.slice(0, 4)); // Limit to 8 for now
      } catch (error) {
        console.error('Error fetching products:', error);
      }
    };

    fetchProducts();
  }, []);

  return (
    <div className="featured-products">
      <h2>Featured Products</h2>
      <div className="product-list">
        {products.map(product => (
          <div key={product.id} className="product-item">
            <img src={product.imageUrl} alt={product.name} />
            <h3>{product.name}</h3>
            
          </div>
        ))}
      </div>
    </div>
  );
};

export default FeaturedProducts;
