import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import API_BASE_URL, { resolveProductImage } from '../config/api';
import './ProductPage.css';

const ProductsPage = () => {
  const [products, setProducts] = useState([]);
  const [managedCategories, setManagedCategories] = useState([]);
  const [filteredProducts, setFilteredProducts] = useState([]);
  const [selectedCategory, setSelectedCategory] = useState(() => new URLSearchParams(window.location.search).get('category') || 'All');
  const [selectedBrand, setSelectedBrand] = useState('All');
  const [searchQuery, setSearchQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [sortOrder, setSortOrder] = useState('featured');
  const [minimumPrice, setMinimumPrice] = useState('');
  const [maximumPrice, setMaximumPrice] = useState('');
  const [reloadKey, setReloadKey] = useState(0);


  useEffect(() => {
    const fetchProducts = async () => {
      try {
        const [res, categoryResponse] = await Promise.all([
          fetch(`${API_BASE_URL}/api/products`),
          fetch(`${API_BASE_URL}/api/categories`).catch(() => null)
        ]);
        if (!res.ok) throw new Error('Failed to load products');
        const data = await res.json();
        setProducts(data);
        setFilteredProducts(data);
        if (categoryResponse?.ok) {
          const categoryData = await categoryResponse.json();
          setManagedCategories(categoryData.filter((category) => category.isActive).map((category) => category.name));
        }
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

    if (selectedBrand !== 'All') {
      result = result.filter((p) => p.brand?.toLowerCase() === selectedBrand.toLowerCase());
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(
        (p) => p.name?.toLowerCase().includes(q) || p.description?.toLowerCase().includes(q) || p.brand?.toLowerCase().includes(q)
      );
    }

    const productPrice = (product) => {
      const prices = (product.variants || []).filter((variant) => variant.isActive !== false).map((variant) => Number(variant.salePrice ?? variant.price));
      return prices.length ? Math.min(...prices) : Number(product.salePrice ?? product.price);
    };
    if (minimumPrice !== '') result = result.filter((product) => productPrice(product) >= Number(minimumPrice));
    if (maximumPrice !== '') result = result.filter((product) => productPrice(product) <= Number(maximumPrice));

    if (sortOrder === 'price-low') result = [...result].sort((a, b) => productPrice(a) - productPrice(b));
    if (sortOrder === 'price-high') result = [...result].sort((a, b) => productPrice(b) - productPrice(a));
    if (sortOrder === 'featured') result = [...result].sort((a, b) => Number(b.isFeatured) - Number(a.isFeatured));
    setFilteredProducts(result);
  }, [selectedCategory, selectedBrand, searchQuery, products, sortOrder, minimumPrice, maximumPrice]);

  // Extract distinct categories
  const categories = ['All', ...new Set([...managedCategories, ...products.map((p) => p.category)].filter(Boolean))];
  const brands = ['All', ...new Set(products.map((p) => p.brand).filter(Boolean))];

  return (
    <div className="products-page">
      <div className="products-hero">
        <h2>Discover the Belah collection</h2>
        <p className="products-subtitle">
          Explore fragrances, accessories, beauty, and new finds from Belah.
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
            placeholder="Search products or brands..."
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
        {brands.length > 1 && <label className="catalog-sort">Brand
          <select aria-label="Filter by brand" value={selectedBrand} onChange={(e) => setSelectedBrand(e.target.value)}>
            {brands.map((brand) => <option key={brand} value={brand}>{brand === 'All' ? 'All brands' : brand}</option>)}
          </select>
        </label>}
        <label className="catalog-sort">Minimum price (ZAR)
          <input type="number" min="0" step="0.01" value={minimumPrice} onChange={(e) => setMinimumPrice(e.target.value)} aria-label="Minimum price" />
        </label>
        <label className="catalog-sort">Maximum price (ZAR)
          <input type="number" min="0" step="0.01" value={maximumPrice} onChange={(e) => setMaximumPrice(e.target.value)} aria-label="Maximum price" />
        </label>
      </div>

      {loading && <div className="products-state">Loading luxury products...</div>}
      {error && <div className="products-state error" role="alert"><p>{error}</p><button className="products-retry-btn" type="button" onClick={() => { setLoading(true); setError(''); setReloadKey((key) => key + 1); }}>Try again</button></div>}

      {!loading && !error && (
        <div className="product-grid">
          {filteredProducts.length > 0 ? (
            filteredProducts.map((product) => {
              const variants = (product.variants || []).filter((variant) => variant.isActive !== false);
              const stockQuantity = variants.length ? variants.reduce((sum, variant) => sum + variant.stockQuantity, 0) : (product.stockQuantity ?? 0);
              const pricedVariants = variants.map((variant) => ({ regular: Number(variant.price), current: Number(variant.salePrice ?? variant.price) })).filter((price) => Number.isFinite(price.regular) && Number.isFinite(price.current));
              const prices = pricedVariants.map((price) => price.current);
              const isStockConfirmed = product.isStockConfirmed !== false;
              const isPriceApproved = product.isPriceApproved !== false;
              const isOutOfStock = isStockConfirmed && stockQuantity <= 0;
              const lowestCurrentVariant = pricedVariants.length ? pricedVariants.reduce((lowest, candidate) => candidate.current < lowest.current ? candidate : lowest) : null;
              const currentPrice = lowestCurrentVariant?.current ?? Number(product.salePrice ?? product.price);
              const regularPrice = lowestCurrentVariant?.regular ?? Number(product.price);
              const isOnSale = isPriceApproved && currentPrice < regularPrice;
              const pricePrefix = prices.length > 1 && Math.min(...prices) !== Math.max(...prices) ? 'From ' : '';
              const priceLabel = !isPriceApproved
                ? (Number(product.price) > 0 ? `Indicative · R${Number(product.price).toFixed(2)}` : 'Price pending approval')
                : `${pricePrefix}R${currentPrice.toFixed(2)}`;
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
                    {!isStockConfirmed && <span className="sold-out-overlay">Availability unconfirmed</span>}
                    {isStockConfirmed && isOutOfStock && <span className="sold-out-overlay">Sold Out</span>}
                  </Link>

                  <div className="card-body">
                    {product.category && <span className="card-category">{product.category}</span>}
                    {product.brand && <span className="card-brand">{product.brand}</span>}
                    <Link to={`/product/${product.id}`} className="card-title-link">
                      <h3 className="card-title">{product.name}</h3>
                    </Link>
                    <div className={`card-price${isOnSale ? ' sale-price' : ''}`}>{isOnSale && <del>R{regularPrice.toFixed(2)}</del>}<strong>{priceLabel}</strong>{isOnSale && <span className="sale-badge">Save R{(regularPrice - currentPrice).toFixed(2)}</span>}</div>
                    {!isPriceApproved && Number(product.price) > 0 && <small>Indicative reference only · approval required</small>}

                    <div className="card-stock">
                      {!isStockConfirmed ? (
                        <span className="stock-badge out-of-stock">Availability unconfirmed</span>
                      ) : isOutOfStock ? (
                        <span className="stock-badge out-of-stock">Out of Stock</span>
                      ) : (
                        <span className="stock-badge in-stock">
                          In Stock ({stockQuantity} available)
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
