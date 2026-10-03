import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './CartPage.css';

const CartPage = () => {
  const [cartItems, setCartItems] = useState([]);
  const navigate = useNavigate();

  useEffect(() => {
    const storedCart = JSON.parse(localStorage.getItem('cart')) || [];
    setCartItems(storedCart);
  }, []);

  const updateCart = (items) => {
    setCartItems(items);
    localStorage.setItem('cart', JSON.stringify(items));
  };

  const handleIncrease = (id, color, storage) => {
    const updated = cartItems.map(item =>
      item.id === id &&
      item.selectedColor === color &&
      item.selectedStorage === storage
        ? { ...item, quantity: item.quantity + 1 }
        : item
    );
    updateCart(updated);
  };

  const handleDecrease = (id, color, storage) => {
    const updated = cartItems.map(item =>
      item.id === id &&
      item.selectedColor === color &&
      item.selectedStorage === storage
        ? { ...item, quantity: Math.max(item.quantity - 1, 1) }
        : item
    );
    updateCart(updated);
  };

  const handleRemove = (id, color, storage) => {
    const updated = cartItems.filter(
      item =>
        !(item.id === id &&
          item.selectedColor === color &&
          item.selectedStorage === storage)
    );
    updateCart(updated);
  };

  const totalPrice = cartItems.reduce((sum, item) => sum + item.price * item.quantity, 0);

  return (
    <div className="cart-container fade-in">
      <h2>Your Shopping Cart</h2>

      {cartItems.length === 0 ? (
        <p>Your cart is empty.</p>
      ) : (
        <>
          <ul className="cart-list">
            {cartItems.map(item => (
              <li
                key={`${item.id}-${item.selectedColor}-${item.selectedStorage}`}
                className="cart-item"
              >
                <img src={item.imageUrl} alt={item.name} className="cart-image" />
                <div className="cart-details">
                  <h3>{item.name}</h3>

                  {/* Color swatch */}
                  {item.selectedColor && (
                    <div className="cart-color">
                      <strong>Color:</strong>
                      <span
                        className="color-swatch"
                        style={{ backgroundColor: item.selectedColor.toLowerCase() }}
                        title={item.selectedColor}
                      ></span>
                      {item.selectedColor}
                    </div>
                  )}

                  {/* Storage */}
                  {item.selectedStorage && (
                    <p><strong>Storage:</strong> {item.selectedStorage}</p>
                  )}

                  <p>ZAR {item.price} x {item.quantity}</p>

                  <div className="cart-controls">
                    <button
                      className="qty-btn"
                      onClick={() => handleDecrease(item.id, item.selectedColor, item.selectedStorage)}
                    >
                      −
                    </button>
                    <span className="qty">{item.quantity}</span>
                    <button
                      className="qty-btn"
                      onClick={() => handleIncrease(item.id, item.selectedColor, item.selectedStorage)}
                    >
                      +
                    </button>
                    <button
                      onClick={() => handleRemove(item.id, item.selectedColor, item.selectedStorage)}
                      className="remove-btn"
                    >
                      Remove
                    </button>
                  </div>
                </div>
              </li>
            ))}
          </ul>

          <div className="cart-summary">
            <h3>Total: ZAR {totalPrice.toFixed(2)}</h3>
            <button className="checkout-btn" onClick={() => navigate('/checkout')}>
              Proceed to Checkout
            </button>
          </div>
        </>
      )}
    </div>
  );
};

export default CartPage;
