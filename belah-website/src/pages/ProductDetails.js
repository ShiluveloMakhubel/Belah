import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import './ProductDetails.css';

const ProductDetails = () => {
  const { productId } = useParams();
  const navigate = useNavigate();
  const [product, setProduct] = useState(null);
  const [loading, setLoading] = useState(true);

  const [selectedColor, setSelectedColor] = useState('');
  const [selectedStorage, setSelectedStorage] = useState('');

  const colorOptions = [
    { name: 'Black', code: '#000000' },
    { name: 'White', code: '#FFFFFF' },
    { name: 'Silver', code: '#C0C0C0' },
    { name: 'Gold', code: '#FFD700' },
    { name: 'Blue', code: '#1E90FF' },
    { name: 'Red', code: '#FF0000' }
  ];

  const storageOptions = ['64GB', '128GB', '256GB', '512GB', '1TB'];

  useEffect(() => {
    const fetchProduct = async () => {
      try {
        const res = await fetch(`http://localhost:5000/api/products/${productId}`);
        if (!res.ok) throw new Error('Product not found');
        const data = await res.json();
        setProduct(data);
      } catch (error) {
        console.error(error);
        setProduct(null);
      } finally {
        setLoading(false);
      }
    };

    fetchProduct();
  }, [productId]);

  const handleAddToCart = () => {
    if (!selectedColor || !selectedStorage) {
      alert('Please select both color and storage before adding to cart.');
      return;
    }

    const cart = JSON.parse(localStorage.getItem('cart')) || [];
    const itemExists = cart.find(
      p =>
        p.id === product.id &&
        p.selectedColor === selectedColor &&
        p.selectedStorage === selectedStorage
    );

    if (!itemExists) {
      cart.push({ ...product, selectedColor, selectedStorage, quantity: 1 });
    } else {
      itemExists.quantity += 1;
    }

    localStorage.setItem('cart', JSON.stringify(cart));
    alert(`${product.name} (${selectedColor}, ${selectedStorage}) added to cart.`);
  };

  const handleBack = () => {
    navigate(-1);
  };

  if (loading) return <div>Loading...</div>;
  if (!product) return <div>Product not found</div>;

  return (
    <div className="product-details fade-in">
      <button className="back-btn" onClick={handleBack}>← Back</button>

      <img src={product.imageUrl} alt={product.name} className="product-image" />

      <div className="product-info">
        <h2>{product.name}</h2>
        <p><strong>Price:</strong> {product.price}</p>
        <p><strong>Category:</strong> {product.category}</p>
        <p><strong>Description:</strong> {product.description}</p>
        <p><strong>Specifications:</strong> {product.specifications}</p>
        <p><strong>Rating:</strong> {product.rating} / 5</p>

        {/* Color Selection */}
        <div className="option-group">
          <label><strong>Color:</strong></label>
          <div className="color-options">
            {colorOptions.map(color => (
              <div
                key={color.name}
                className={`color-swatch ${selectedColor === color.name ? 'selected' : ''}`}
                style={{ backgroundColor: color.code }}
                title={color.name}
                onClick={() => setSelectedColor(color.name)}
              ></div>
            ))}
          </div>
        </div>

        {/* Storage Selection */}
        <div className="option-group">
          <label><strong>Storage:</strong></label>
          <div className="storage-options">
            {storageOptions.map(storage => (
              <button
                key={storage}
                className={`storage-btn ${selectedStorage === storage ? 'selected' : ''}`}
                onClick={() => setSelectedStorage(storage)}
              >
                {storage}
              </button>
            ))}
          </div>
        </div>

        <button className="add-to-cart-btn" onClick={handleAddToCart}>
          Add to Cart
        </button>
      </div>
    </div>
  );
};

export default ProductDetails;
