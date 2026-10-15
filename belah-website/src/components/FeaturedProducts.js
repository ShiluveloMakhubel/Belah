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
        const featured = data.filter((product) => product.isFeatured);
        setProducts((featured.length ? featured : data).slice(0, 4));
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
            {(() => {
              const variants = (product.variants || []).filter((variant) => variant.isActive !== false);
              const lowestVariant = variants.length ? variants.reduce((lowest, variant) => Number(variant.salePrice ?? variant.price) < Number(lowest.salePrice ?? lowest.price) ? variant : lowest) : null;
              const regularPrice = Number(lowestVariant?.price ?? product.price ?? 0);
              const salePrice = lowestVariant ? lowestVariant.salePrice : product.salePrice;
              const currentPrice = Number(salePrice ?? regularPrice);
              const isOnSale = product.isPriceApproved !== false && salePrice != null && currentPrice < regularPrice;
              const label = product.isPriceApproved === false ? (Number(product.price) > 0 ? `Indicative · R${Number(product.price).toFixed(2)}` : 'Price pending approval') : `${variants.length ? 'From ' : ''}R${currentPrice.toFixed(2)}`;
              return <p className={`featured-product-price${isOnSale ? ' sale-price' : ''}`}>{isOnSale && <del>R{regularPrice.toFixed(2)}</del>}<strong>{label}</strong>{isOnSale && <span className="sale-badge">Save R{(regularPrice - currentPrice).toFixed(2)}</span>}</p>;
            })()}
          </Link>
        ))}
      </div>
    </div>
  );
};

export default FeaturedProducts;
