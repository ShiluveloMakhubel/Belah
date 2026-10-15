import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import API_BASE_URL, { resolveProductImage } from '../config/api';
import { DELIVERY_FEE_ZAR } from '../config/commerce';
import './CartPage.css';

const CartPage = () => {
  const [cartItems, setCartItems] = useState([]);
  const [stockMap, setStockMap] = useState({});
  const [loadingStock, setLoadingStock] = useState(false);
  const [stockLoaded, setStockLoaded] = useState(false);
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
            variants: Object.fromEntries((p.variants || []).map((variant) => [variant.id, variant])),
          };
        });
        setStockMap(map);
        setStockLoaded(true);
        const updatedCart = storedCart.map((item) => {
          const product = products.find((p) => p.id === item.id);
          if (!product) return item;
          const variant = item.variantId ? (product.variants || []).find((v) => v.id === item.variantId) : null;
          const regularPrice = Number(variant?.price ?? product.price ?? item.price);
          const salePrice = product.isPriceApproved === false ? null : variant ? variant.salePrice : product.salePrice;
          return { ...item, regularPrice, price: salePrice != null ? Number(salePrice) : regularPrice };
        });
        setCartItems(updatedCart);
        localStorage.setItem('cart', JSON.stringify(updatedCart));
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
  const sameCartOption = (left, right) => left.id === right.id && (left.variantId || null) === (right.variantId || null);

  const getTotalQuantityInCart = (productId, variantId = null, excludeItem = null) => {
    return cartItems
      .filter(
        (item) =>
          item.id === productId && (item.variantId || null) === (variantId || null) &&
          !(
            excludeItem &&
            sameCartOption(item, excludeItem)
          )
      )
      .reduce((sum, item) => sum + (item.quantity || 0), 0);
  };

  const getAvailableStock = (item, fallbackQuantity = 0) => {
    if (stockMap[item.id] === undefined) return stockLoaded ? 0 : fallbackQuantity;
    if (!stockMap[item.id].isActive) return 0;
    if (item.variantId) return stockMap[item.id].variants?.[item.variantId]?.stockQuantity ?? 0;
    return stockMap[item.id].stockQuantity;
  };

  const handleIncrease = (target) => {
    const targetItem = cartItems.find(
      (item) => sameCartOption(item, target)
    );

    if (!targetItem) return;

    const availableStock = getAvailableStock(targetItem, targetItem.stockQuantity ?? 999);
    const totalInCartForProduct = getTotalQuantityInCart(targetItem.id, targetItem.variantId);

    if (totalInCartForProduct + 1 > availableStock) {
      alert(
        `Cannot increase quantity. Only ${availableStock} item(s) are available in stock for this product.`
      );
      return;
    }

    const updated = cartItems.map((item) =>
      sameCartOption(item, targetItem)
        ? { ...item, quantity: item.quantity + 1 }
        : item
    );
    updateCart(updated);
  };

  const handleDecrease = (target) => {
    const updated = cartItems.map((item) =>
      sameCartOption(item, target)
        ? { ...item, quantity: Math.max(item.quantity - 1, 1) }
        : item
    );
    updateCart(updated);
  };

  const handleRemove = (target) => {
    const updated = cartItems.filter(
      (item) =>
        !(
          sameCartOption(item, target)
        )
    );
    updateCart(updated);
  };

  // Check if cart has any stock violations
  const hasStockViolations = cartItems.some((item) => {
    const availableStock = getAvailableStock(item, item.stockQuantity ?? 0);
    const totalInCart = getTotalQuantityInCart(item.id, item.variantId);
    return availableStock <= 0 || totalInCart > availableStock;
  });

  const handleProceedToCheckout = () => {
    if (cartItems.length === 0) {
      alert('Your cart is empty.');
      return;
    }

    // Validate quantities against stock
    for (const item of cartItems) {
      const availableStock = getAvailableStock(item, item.stockQuantity ?? 0);
      const totalInCart = getTotalQuantityInCart(item.id, item.variantId);

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
  const orderTotal = totalPrice + DELIVERY_FEE_ZAR;

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
              const availableStock = getAvailableStock(item, item.stockQuantity ?? 0);
              const totalInCart = getTotalQuantityInCart(item.id, item.variantId);
              const isOutOfStock = availableStock <= 0;
              const exceedsStock = totalInCart > availableStock;

              return (
                <li
                  key={`${item.id}-${item.variantId || 'base'}`}
                  className="cart-item"
                >
                  <img src={resolveProductImage(item.imageUrl)} alt={item.name} className="cart-image" onError={(event) => { event.currentTarget.onerror = null; event.currentTarget.src = '/images/product-placeholder.svg'; }} />
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
                    {item.selectedVariantName && (
                      <p>
                        <strong>Size / option:</strong> {item.selectedVariantName}
                      </p>
                    )}

                    <p className="cart-price-line">
                      {item.regularPrice > item.price && <del aria-label={`Regular price ZAR ${Number(item.regularPrice).toFixed(2)}`}>ZAR {Number(item.regularPrice).toFixed(2)}</del>}
                      <strong>ZAR {Number(item.price).toFixed(2)}</strong> × {item.quantity}
                    </p>

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
                          handleDecrease(item)
                        }
                      >
                        −
                      </button>
                      <span className="qty">{item.quantity}</span>
                      <button
                        className="qty-btn"
                        onClick={() =>
                          handleIncrease(item)
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
                          handleRemove(item)
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
            <p>Items subtotal: <strong>ZAR {totalPrice.toFixed(2)}</strong></p>
            <p>Fixed delivery fee: <strong>ZAR {DELIVERY_FEE_ZAR.toFixed(2)}</strong></p>
            <h3>Total: ZAR {orderTotal.toFixed(2)}</h3>
            <p className="delivery-fee-note">Delivery fee is fixed for every order and is included before payment.</p>
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
