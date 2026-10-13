import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import API_BASE_URL, { resolveProductImage } from '../config/api';
import './ProductPage.css';

const ProductsPage = () => {
  const [products, setProducts] = useState([]);
  const [filteredProducts, setFilteredProducts] = useState([]);
  const [selectedCategory, setSelectedCategory] = useState('All');
  const [searchQuery, setSearchQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [sortOrder, setSortOrder] = useState('featured');
  const [reloadKey, setReloadKey] = useState(0);


  useEffect(() => {
    const fetchProducts = async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/api/products`);
        if (!res.ok) throw new Error('Failed to load products');
        const data = await res.json();
        setProducts(data);
        setFilteredProducts(data);
      } catch (err) {
        console.error('Failed to fetch products:', err);
        setError('Could not load products. Please check back shortly.');
      } finally {
        setLoading(false);
      }
    };

    fetchProducts();
  }, [reloadKey]);

  // Filter by category and search term
  useEffect(() => {
    let result = products;

    if (selectedCategory !== 'All') {
      result = result.filter(
        (p) => p.category?.toLowerCase() === selectedCategory.toLowerCase()
      );
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(
        (p) => p.name?.toLowerCase().includes(q) || p.description?.toLowerCase().includes(q)
      );
    }

    if (sortOrder === 'price-low') result = [...result].sort((a, b) => a.price - b.price);
    if (sortOrder === 'price-high') result = [...result].sort((a, b) => b.price - a.price);
    setFilteredProducts(result);
  }, [selectedCategory, searchQuery, products, sortOrder]);

  // Extract distinct categories
  const categories = ['All', ...new Set(products.map((p) => p.category).filter(Boolean))];

  return (
    <div className="products-page">
      <div className="products-hero">
        <h2>Belah Luxury Collection</h2>
        <p className="products-subtitle">
          Discover 100% premium virgin hair, HD lace frontals, extensions, and styling essentials.
        </p>
      </div>

      <div className="products-filter-bar">
        <div className="category-chips">
          {categories.map((cat) => (
            <button
              key={cat}
              className={`category-chip ${selectedCategory === cat ? 'active' : ''}`}
              aria-pressed={selectedCategory === cat}
              onClick={() => setSelectedCategory(cat)}
            >
              {cat}
            </button>
          ))}
        </div>

        <div className="search-input-wrapper">
          <input
            aria-label="Search products"
            type="text"
            placeholder="Search wigs, frontals, bundles..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>
        <label className="catalog-sort">Sort products
          <select value={sortOrder} onChange={(e) => setSortOrder(e.target.value)}>
            <option value="featured">Featured</option>
            <option value="price-low">Price: low to high</option>
            <option value="price-high">Price: high to low</option>
          </select>
        </label>
      </div>

      {loading && <div className="products-state">Loading luxury products...</div>}
      {error && <div className="products-state error" role="alert"><p>{error}</p><button type="button" onClick={() => { setLoading(true); setError(''); setReloadKey((key) => key + 1); }}>Try again</button></div>}

      {!loading && !error && (
        <div className="product-grid">
          {filteredProducts.length > 0 ? (
            filteredProducts.map((product) => {
              const isOutOfStock = (product.stockQuantity ?? 0) <= 0;
              return (
                <article
                  key={product.id}
                  className={`product-card ${isOutOfStock ? 'is-out-of-stock' : ''}`}
                >
                  <Link to={`/product/${product.id}`} className="card-image-link">
                    <img
                      src={resolveProductImage(product.imageUrl)}
                      alt={product.name}
                      className="card-image"
                      onError={(e) => {
                        e.target.onerror = null;
                        e.target.src = '/images/product-placeholder.svg';
                      }}
                    />
                    {isOutOfStock && <span className="sold-out-overlay">Sold Out</span>}
                  </Link>

                  <div className="card-body">
                    {product.category && <span className="card-category">{product.category}</span>}
                    <Link to={`/product/${product.id}`} className="card-title-link">
                      <h3 className="card-title">{product.name}</h3>
                    </Link>
                    <div className="card-price">R{Number(product.price).toFixed(2)}</div>

                    <div className="card-stock">
                      {isOutOfStock ? (
                        <span className="stock-badge out-of-stock">Out of Stock</span>
                      ) : (
                        <span className="stock-badge in-stock">
                          In Stock ({product.stockQuantity} available)
                        </span>
                      )}
                    </div>

                    <Link to={`/product/${product.id}`} className="view-product-btn">
                      View Details
                    </Link>
                  </div>
                </article>
              );
            })
          ) : (
            <div className="products-empty-state">
              <h3>No products found</h3>
              <p>Try searching for a different keyword or select another category.</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default ProductsPage;
