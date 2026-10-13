import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import API_BASE_URL, { resolveProductImage } from '../config/api';
import './ProductDetails.css';

const ProductDetails = () => {
  const { productId } = useParams();
  const navigate = useNavigate();
  const [product, setProduct] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');


  useEffect(() => {
    const fetchProduct = async () => {
      setLoading(true);
      setProduct(null);
      setLoadError('');
      try {
        const res = await fetch(`${API_BASE_URL}/api/products/${productId}`);
        if (!res.ok) throw new Error(res.status === 404 ? 'Product not found' : 'Product API unavailable');
        const data = await res.json();
        setProduct(data);
      } catch (error) {
        console.error(error);
        setProduct(null);
        setLoadError(error.message === 'Product not found' ? 'This product is not found or is currently unavailable.' : 'We could not load this product. Check your connection and retry.');
      } finally {
        setLoading(false);
      }
    };

    fetchProduct();
  }, [productId]);

  const handleAddToCart = () => {
    if (!product) return;

    const availableStock = product.stockQuantity ?? 0;
    if (availableStock <= 0) {
      alert('This product is currently out of stock.');
      return;
    }

    const cart = JSON.parse(localStorage.getItem('cart')) || [];

    // Total quantity already in cart for this product across all color/storage variants
    const totalInCartForProduct = cart
      .filter((p) => p.id === product.id)
      .reduce((sum, item) => sum + (item.quantity || 0), 0);

    if (totalInCartForProduct >= availableStock) {
      alert(
        `Cannot add more. You already have ${totalInCartForProduct} in your cart, which reaches the total available stock (${availableStock}).`
      );
      return;
    }

    const itemExists = cart.find(
      (p) =>
        p.id === product.id &&
        !p.selectedColor && !p.selectedStorage
    );

    if (!itemExists) {
      cart.push({ ...product, selectedColor: null, selectedStorage: null, quantity: 1 });
    } else {
      itemExists.quantity += 1;
    }

    localStorage.setItem('cart', JSON.stringify(cart));
    alert(`${product.name} added to cart.`);
  };

  const handleBack = () => {
    navigate(-1);
  };

  if (loading) return <div role="status">Loading product details…</div>;
  if (!product) return <div role="alert">{loadError || 'Product not found or is currently unavailable.'} <button type="button" onClick={() => window.location.reload()}>Retry</button></div>;

  const isOutOfStock = (product.stockQuantity ?? 0) <= 0;

  return (
    <div className="product-details fade-in">
      <button className="back-btn" onClick={handleBack}>← Back</button>

      <img src={resolveProductImage(product.imageUrl)} alt={product.name} className="product-image" onError={(e) => { e.currentTarget.onerror = null; e.currentTarget.src = '/images/product-placeholder.svg'; }} />

      <div className="product-info">
        <h2>{product.name}</h2>
        <p><strong>Price:</strong> ZAR {Number(product.price).toFixed(2)}</p>
        <p><strong>Category:</strong> {product.category}</p>
        {product.sku && <p><strong>SKU:</strong> {product.sku}</p>}
        <p>
          <strong>Availability:</strong>{' '}
          {isOutOfStock ? (
            <span className="stock-out">Out of Stock</span>
          ) : (
            <span className="stock-in">In Stock ({product.stockQuantity} available)</span>
          )}
        </p>
        <p><strong>Description:</strong> {product.description}</p>
        {product.specification && <p><strong>Specifications:</strong> {product.specification}</p>}
        {product.rating && <p><strong>Rating:</strong> {product.rating} / 5</p>}

        <button
          className="add-to-cart-btn"
          onClick={handleAddToCart}
          disabled={isOutOfStock}
        >
          {isOutOfStock ? 'Out of Stock' : 'Add to Cart'}
        </button>
      </div>
    </div>
  );
};

export default ProductDetails;
