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
  const [selectedVariantId, setSelectedVariantId] = useState('');
  const [cartMessage, setCartMessage] = useState('');


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
        const availableVariants = (data.variants || []).filter((variant) => variant.isActive !== false);
        setSelectedVariantId(availableVariants.length === 1 ? String(availableVariants[0].id) : '');
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
    setCartMessage('');

    const variants = (product.variants || []).filter((variant) => variant.isActive !== false);
    const selectedVariant = variants.find((variant) => String(variant.id) === selectedVariantId);
    if (variants.length > 0 && !selectedVariant) {
      setCartMessage('Select an available size or option before adding this product to your cart.');
      return;
    }
    const availableStock = selectedVariant ? selectedVariant.stockQuantity : (product.stockQuantity ?? 0);
    if (product.isPriceApproved === false || product.isStockConfirmed === false) {
      setCartMessage('This listing is not available for purchase until its price and stock are confirmed.');
      return;
    }
    if (availableStock <= 0) {
      setCartMessage('This product is currently out of stock.');
      return;
    }

    const cart = JSON.parse(localStorage.getItem('cart')) || [];

    // Total quantity already in cart for this product across all color/storage variants
    const totalInCartForProduct = cart
      .filter((p) => p.id === product.id && (p.variantId || null) === (selectedVariant?.id || null))
      .reduce((sum, item) => sum + (item.quantity || 0), 0);

    if (totalInCartForProduct >= availableStock) {
      setCartMessage(`Your cart already contains ${totalInCartForProduct}, which is the available stock (${availableStock}).`);
      return;
    }

    const itemExists = cart.find(
      (p) =>
        p.id === product.id &&
        (p.variantId || null) === (selectedVariant?.id || null)
    );

    if (!itemExists) {
      const regularPrice = selectedVariant?.price ?? product.price;
      const price = selectedVariant?.salePrice ?? (selectedVariant ? regularPrice : product.salePrice ?? regularPrice);
      cart.push({ ...product, price, regularPrice, stockQuantity: availableStock, variantId: selectedVariant?.id ?? null, selectedVariantName: selectedVariant?.name ?? null, imageUrl: selectedVariant?.imageUrl || product.imageUrl, quantity: 1 });
    } else {
      itemExists.quantity += 1;
      itemExists.regularPrice = selectedVariant?.price ?? product.price;
      itemExists.price = selectedVariant?.salePrice ?? (selectedVariant ? itemExists.regularPrice : product.salePrice ?? itemExists.regularPrice);
    }

    localStorage.setItem('cart', JSON.stringify(cart));
    setCartMessage(`${product.name}${selectedVariant ? ` · ${selectedVariant.name}` : ''} added to your cart.`);
  };

  const handleBack = () => {
    navigate(-1);
  };

  if (loading) return <div role="status">Loading product details…</div>;
  if (!product) return <div role="alert">{loadError || 'Product not found or is currently unavailable.'} <button type="button" onClick={() => window.location.reload()}>Retry</button></div>;

  const variants = (product.variants || []).filter((variant) => variant.isActive !== false);
  const selectedVariant = variants.find((variant) => String(variant.id) === selectedVariantId);
  const isStockConfirmed = product.isStockConfirmed !== false;
  const isPriceApproved = product.isPriceApproved !== false;
  const isOutOfStock = variants.length > 0
    ? (selectedVariant ? selectedVariant.stockQuantity <= 0 : variants.every((variant) => variant.stockQuantity <= 0))
    : (product.stockQuantity ?? 0) <= 0;
  const needsVariantSelection = variants.length > 0 && !selectedVariant;
  const lowestCurrentVariant = variants.length
    ? variants.reduce((lowest, variant) => Number(variant.salePrice ?? variant.price) < Number(lowest.salePrice ?? lowest.price) ? variant : lowest)
    : null;
  const displayedRegularPrice = selectedVariant?.price ?? lowestCurrentVariant?.price ?? Number(product.price);
  const displayedPrice = !isPriceApproved ? displayedRegularPrice : selectedVariant?.salePrice ?? (selectedVariant ? selectedVariant.price : lowestCurrentVariant?.salePrice ?? lowestCurrentVariant?.price ?? product.salePrice ?? product.price);

  return (
    <div className="product-details fade-in">
      <button className="back-btn" onClick={handleBack}>← Back</button>

      <img src={resolveProductImage(product.imageUrl)} alt={product.name} className="product-image" onError={(e) => { e.currentTarget.onerror = null; e.currentTarget.src = '/images/product-placeholder.svg'; }} />

      <div className="product-info">
        <h2>{product.name}</h2>
        {product.brand && <p><strong>Brand:</strong> {product.brand}</p>}
        <p className="product-detail-price"><strong>Price:</strong> {!isPriceApproved
          ? (Number(displayedPrice) > 0 ? `Indicative ZAR ${Number(displayedPrice).toFixed(2)} (approval required)` : 'Pending approval')
          : <>{displayedPrice < displayedRegularPrice && <del>ZAR {Number(displayedRegularPrice).toFixed(2)}</del>} <strong>ZAR {Number(displayedPrice).toFixed(2)}{variants.length > 1 && !selectedVariant ? ' (starting price)' : ''}</strong>{displayedPrice < displayedRegularPrice && <span className="sale-badge">Save R{(displayedRegularPrice - displayedPrice).toFixed(2)}</span>}</>}</p>
        <p><strong>Category:</strong> {product.category}</p>
        {variants.length > 0 && <label className="variant-select">Size or option
          <select aria-label="Select product size or option" value={selectedVariantId} onChange={(event) => setSelectedVariantId(event.target.value)}>
            <option value="">Choose an option</option>
            {variants.map((variant) => <option key={variant.id} value={variant.id} disabled={variant.stockQuantity <= 0}>
              {variant.name} — {variant.salePrice != null ? `R${Number(variant.salePrice).toFixed(2)} (was R${Number(variant.price).toFixed(2)})` : `R${Number(variant.price).toFixed(2)}`}{variant.stockQuantity <= 0 ? ' (Out of stock)' : ''}
            </option>)}
          </select>
        </label>}
        {product.sku && <p><strong>SKU:</strong> {product.sku}</p>}
        <p>
          <strong>Availability:</strong>{' '}
          {!isStockConfirmed ? (
            <span className="stock-out">Availability unconfirmed</span>
          ) : isOutOfStock ? (
            <span className="stock-out">Out of Stock</span>
          ) : (
            <span className="stock-in">In Stock ({selectedVariant?.stockQuantity ?? variants.reduce((sum, variant) => sum + variant.stockQuantity, 0)} available)</span>
          )}
        </p>
        <p><strong>Description:</strong> {product.description}</p>
        {product.specification && <p><strong>Specifications:</strong> {product.specification}</p>}
        {product.rating && <p><strong>Rating:</strong> {product.rating} / 5</p>}

        <button
          className="add-to-cart-btn"
          onClick={handleAddToCart}
          disabled={!isPriceApproved || !isStockConfirmed || isOutOfStock || needsVariantSelection}
        >
          {!isPriceApproved ? 'Price pending approval' : !isStockConfirmed ? 'Availability unconfirmed' : isOutOfStock ? 'Out of Stock' : needsVariantSelection ? 'Choose a size or option' : 'Add to Cart'}
        </button>
        {cartMessage && <p className="cart-feedback" role="status">{cartMessage}</p>}
      </div>
    </div>
  );
};

export default ProductDetails;
