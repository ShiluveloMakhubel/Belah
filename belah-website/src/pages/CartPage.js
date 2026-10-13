import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import API_BASE_URL from '../config/api';
import './CartPage.css';

const CartPage = () => {
  const [cartItems, setCartItems] = useState([]);
  const [stockMap, setStockMap] = useState({});
  const [loadingStock, setLoadingStock] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    const storedCart = JSON.parse(localStorage.getItem('cart')) || [];
    setCartItems(storedCart);

    // Fetch live product stock from backend
    const fetchStock = async () => {
      try {
        setLoadingStock(true);
        const res = await fetch(`${API_BASE_URL}/api/products`);
        if (!res.ok) return;
        const products = await res.json();
        const map = {};
        products.forEach((p) => {
          map[p.id] = {
            stockQuantity: p.stockQuantity ?? 0,
            isActive: p.isActive !== false,
            name: p.name,
          };
        });
        setStockMap(map);
      } catch (err) {
        console.error('Failed to fetch latest stock info:', err);
      } finally {
        setLoadingStock(false);
      }
    };

    fetchStock();
  }, []);

  const updateCart = (items) => {
    setCartItems(items);
    localStorage.setItem('cart', JSON.stringify(items));
  };

  // Helper to get total quantity of a product in the cart
  const getTotalQuantityInCart = (productId, excludeItem = null) => {
    return cartItems
      .filter(
        (item) =>
          item.id === productId &&
          !(
            excludeItem &&
            item.id === excludeItem.id &&
            item.selectedColor === excludeItem.selectedColor &&
            item.selectedStorage === excludeItem.selectedStorage
          )
      )
      .reduce((sum, item) => sum + (item.quantity || 0), 0);
  };

  const getAvailableStock = (productId, fallbackQuantity = 0) => {
    if (stockMap[productId] !== undefined) {
      return stockMap[productId].stockQuantity;
    }
    return fallbackQuantity;
  };

  const handleIncrease = (id, color, storage) => {
    const targetItem = cartItems.find(
      (item) =>
        item.id === id &&
        item.selectedColor === color &&
        item.selectedStorage === storage
    );

    if (!targetItem) return;

    const availableStock = getAvailableStock(id, targetItem.stockQuantity ?? 999);
    const totalInCartForProduct = getTotalQuantityInCart(id);

    if (totalInCartForProduct + 1 > availableStock) {
      alert(
        `Cannot increase quantity. Only ${availableStock} item(s) are available in stock for this product.`
      );
      return;
    }

    const updated = cartItems.map((item) =>
      item.id === id &&
      item.selectedColor === color &&
      item.selectedStorage === storage
        ? { ...item, quantity: item.quantity + 1 }
        : item
    );
    updateCart(updated);
  };

  const handleDecrease = (id, color, storage) => {
    const updated = cartItems.map((item) =>
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
      (item) =>
        !(
          item.id === id &&
          item.selectedColor === color &&
          item.selectedStorage === storage
        )
    );
    updateCart(updated);
  };

  // Check if cart has any stock violations
  const hasStockViolations = cartItems.some((item) => {
    const availableStock = getAvailableStock(item.id, item.stockQuantity ?? 0);
    const totalInCart = getTotalQuantityInCart(item.id);
    return availableStock <= 0 || totalInCart > availableStock;
  });

  const handleProceedToCheckout = () => {
    if (cartItems.length === 0) {
      alert('Your cart is empty.');
      return;
    }

    // Validate quantities against stock
    for (const item of cartItems) {
      const availableStock = getAvailableStock(item.id, item.stockQuantity ?? 0);
      const totalInCart = getTotalQuantityInCart(item.id);

      if (availableStock <= 0) {
        alert(
          `"${item.name}" is currently out of stock. Please remove it from your cart before proceeding.`
        );
        return;
      }

      if (totalInCart > availableStock) {
        alert(
          `Your cart contains ${totalInCart} of "${item.name}", but only ${availableStock} are available in stock. Please adjust the quantity before proceeding.`
        );
        return;
      }
    }

    navigate('/checkout');
  };

  const totalPrice = cartItems.reduce(
    (sum, item) => sum + item.price * item.quantity,
    0
  );

  return (
    <div className="cart-container fade-in">
      <h2>Your Shopping Cart</h2>

      {hasStockViolations && (
        <div className="cart-alert">
          Some items in your cart exceed available stock or are out of stock. Please adjust quantities before checking out.
        </div>
      )}

      {cartItems.length === 0 ? (
        <p>Your cart is empty.</p>
      ) : (
        <>
          <ul className="cart-list">
            {cartItems.map((item) => {
              const availableStock = getAvailableStock(item.id, item.stockQuantity ?? 0);
              const totalInCart = getTotalQuantityInCart(item.id);
              const isOutOfStock = availableStock <= 0;
              const exceedsStock = totalInCart > availableStock;

              return (
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
                      <p>
                        <strong>Storage:</strong> {item.selectedStorage}
                      </p>
                    )}

                    <p>ZAR {item.price} x {item.quantity}</p>

                    {/* Stock status indicator */}
                    {isOutOfStock ? (
                      <p className="stock-warning">⚠️ This product is currently out of stock.</p>
                    ) : exceedsStock ? (
                      <p className="stock-warning">
                        ⚠️ Total in cart ({totalInCart}) exceeds available stock ({availableStock}).
                      </p>
                    ) : (
                      <p style={{ fontSize: '0.85rem', color: '#666' }}>
                        In Stock: {availableStock} available
                      </p>
                    )}

                    <div className="cart-controls">
                      <button
                        className="qty-btn"
                        onClick={() =>
                          handleDecrease(item.id, item.selectedColor, item.selectedStorage)
                        }
                      >
                        −
                      </button>
                      <span className="qty">{item.quantity}</span>
                      <button
                        className="qty-btn"
                        onClick={() =>
                          handleIncrease(item.id, item.selectedColor, item.selectedStorage)
                        }
                        disabled={totalInCart >= availableStock}
                        title={
                          totalInCart >= availableStock
                            ? 'Maximum available stock reached'
                            : 'Add one'
                        }
                      >
                        +
                      </button>
                      <button
                        onClick={() =>
                          handleRemove(item.id, item.selectedColor, item.selectedStorage)
                        }
                        className="remove-btn"
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>

          <div className="cart-summary">
            <h3>Total: ZAR {totalPrice.toFixed(2)}</h3>
            <button
              className="checkout-btn"
              onClick={handleProceedToCheckout}
              disabled={hasStockViolations || loadingStock}
            >
              {loadingStock ? 'Checking stock...' : 'Proceed to Checkout'}
            </button>
          </div>
        </>
      )}
    </div>
  );
};

export default CartPage;
