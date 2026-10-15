import React, { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../config/AuthContext';
import { useNavigate } from 'react-router-dom';
import apiClient from '../config/apiClient';
import { resolveProductImage } from '../config/api';
import './AdminDashboard.css';

const AdminDashboard = () => {
  const { user, isAdmin, loading: authLoading } = useAuth();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState('products'); // 'products' | 'orders'
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [newCategoryName, setNewCategoryName] = useState('');
  const [orders, setOrders] = useState([]);
  const [inventoryReport, setInventoryReport] = useState(null);
  const [salesReport, setSalesReport] = useState(null);
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [statusMessage, setStatusMessage] = useState('');
  const [productSearch, setProductSearch] = useState('');
  const [orderSearch, setOrderSearch] = useState('');
  const [orderPaymentFilter, setOrderPaymentFilter] = useState('All');
  const [orderStatusFilter, setOrderStatusFilter] = useState('All');
  const [orderDateFilter, setOrderDateFilter] = useState('');
  const [savingProduct, setSavingProduct] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);

  // New/Edit product form state
  const [editingProduct, setEditingProduct] = useState(null);
  const [showProductModal, setShowProductModal] = useState(false);
  const [productForm, setProductForm] = useState({
    name: '',
    brand: '',
    productType: '',
    description: '',
    price: '',
    category: '',
    categoryId: '',
    specification: '',
    sku: '',
    stockQuantity: 0,
    isActive: true,
    imageUrl: '',
    removeImage: false,
    isFeatured: false,
    isPriceApproved: true,
    isStockConfirmed: true,
    variants: []
  });

  const loadData = useCallback(async () => {
    setLoading(true);
    setError('');
    setStatusMessage('');
    try {
      if (activeTab === 'products') {
        const [productResponse, categoryResponse] = await Promise.all([
          apiClient.get('/api/products?includeInactive=true'),
          apiClient.get('/api/categories?includeInactive=true')
        ]);
        setProducts(productResponse.data);
        setCategories(categoryResponse.data);
      } else if (activeTab === 'orders') {
        const res = await apiClient.get('/api/orders');
        setOrders(res.data);
      } else if (activeTab === 'reports') {
        const [inventory, sales] = await Promise.all([
          apiClient.get('/api/admin/reports/inventory?lowStockThreshold=5'),
          apiClient.get('/api/admin/reports/sales')
        ]);
        setInventoryReport(inventory.data);
        setSalesReport(sales.data);
      }
    } catch (err) {
      if (err.response && (err.response.status === 401 || err.response.status === 403)) setError('Unauthorized: Admin access required.');
      else setError('Failed to fetch data from the server.');
    } finally {
      setLoading(false);
    }
  }, [activeTab]);

  useEffect(() => {
    if (!authLoading) {
      if (!user || !isAdmin) {
        navigate('/login');
      } else {
        loadData();
      }
    }
  }, [authLoading, user, isAdmin, activeTab, loadData, navigate]);

  const handleStockUpdate = async (productId, newStock) => {
    const stockVal = parseInt(newStock, 10);
    if (isNaN(stockVal) || stockVal < 0) {
      alert('Stock must be a positive integer.');
      return;
    }

    try {
      await apiClient.patch(`/api/products/${productId}/stock`, { stockQuantity: stockVal });
      setStatusMessage('Stock updated successfully.');
      loadData();
    } catch (err) {
      alert('Failed to update stock: ' + (err.response?.data?.message || err.message));
    }
  };

  const handleToggleProductStatus = async (productId, currentActive) => {
    try {
      await apiClient.patch(`/api/products/${productId}/status`, { isActive: !currentActive });
      setStatusMessage(`Product ${!currentActive ? 'activated' : 'deactivated'} successfully.`);
      loadData();
    } catch (err) {
      alert('Failed to update product status: ' + (err.response?.data?.message || err.message));
    }
  };

  const handleOrderStatusUpdate = async (orderId, newStatus) => {
    const previous = orders.find((order) => order.orderId === orderId);
    setStatusMessage('');
    setError('');
    try {
      const response = await apiClient.patch(`/api/orders/${orderId}/status`, { status: newStatus });
      setSelectedOrder(response.data);
      await loadData();
      setStatusMessage(`Order #${orderId} status updated to ${newStatus}.`);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to update order status. The saved status was not changed.');
      if (previous) setOrders((current) => current.map((order) => order.orderId === orderId ? previous : order));
    }
  };

  const openProductModal = (product = null) => {
    if (product) {
      setEditingProduct(product);
      setProductForm({
        name: product.name,
        brand: product.brand || '',
        productType: product.productType || '',
        description: product.description,
        price: product.price,
        salePrice: product.salePrice ?? '',
        category: product.category || '',
        categoryId: product.categoryId ? String(product.categoryId) : '',
        specification: product.specification || '',
        sku: product.sku || '',
        stockQuantity: product.stockQuantity,
        isActive: product.isActive,
        imageUrl: product.imageUrl || '',
        removeImage: false,
        isFeatured: product.isFeatured || false,
        isPriceApproved: product.isPriceApproved !== false,
        isStockConfirmed: product.isStockConfirmed !== false,
        variants: (product.variants || []).map((variant) => ({ ...variant, price: String(variant.price), salePrice: variant.salePrice == null ? '' : String(variant.salePrice), stockQuantity: String(variant.stockQuantity) }))
      });
    } else {
      setEditingProduct(null);
      setProductForm({
        name: '',
        brand: '',
        productType: '',
        description: '',
        price: '',
        salePrice: '',
        category: '',
        categoryId: '',
        specification: '',
        sku: '',
        stockQuantity: 0,
        isActive: true,
        imageUrl: '/images/product-placeholder.svg',
        removeImage: false,
        isFeatured: false,
        isPriceApproved: true,
        isStockConfirmed: true,
        variants: []
      });
    }
    setShowProductModal(true);
  };

  const handleSaveProduct = async (e) => {
    e.preventDefault();
    const price = Number(productForm.price);
    const salePrice = productForm.salePrice === '' ? null : Number(productForm.salePrice);
    const stockQuantity = Number(productForm.stockQuantity);
    const validMoney = (value) => Number.isFinite(Number(value)) && Number(value) >= 0 && Number(value) <= 1000000 && Math.round(Number(value) * 100) === Number(value) * 100;
    const validSalePrice = salePrice == null || (validMoney(salePrice) && salePrice > 0 && salePrice < price);
    const validVariants = productForm.variants.every((variant) => {
      const variantSalePrice = variant.salePrice === '' || variant.salePrice == null ? null : Number(variant.salePrice);
      return variant.name.trim() && validMoney(variant.price) && (variantSalePrice == null || (validMoney(variantSalePrice) && variantSalePrice > 0 && variantSalePrice < Number(variant.price))) && Number.isInteger(Number(variant.stockQuantity)) && Number(variant.stockQuantity) >= 0;
    });
    const hasValidBaseInventory = productForm.variants.length > 0 || (validMoney(price) && Number.isInteger(stockQuantity) && stockQuantity >= 0);
    if (!productForm.name.trim() || !productForm.description.trim() || !productForm.category.trim() || !hasValidBaseInventory || !validSalePrice || !validVariants) {
      setError('Enter valid product details. A sale price must be lower than its regular price, and stock must be a whole non-negative quantity.');
      return;
    }
    setSavingProduct(true);
    setError('');
    try {
      const payload = {
        name: productForm.name.trim(),
        brand: productForm.brand.trim(),
        productType: productForm.productType.trim(),
        description: productForm.description.trim(),
        price,
        salePrice: productForm.variants.length === 0 ? salePrice : null,
        category: productForm.category.trim(),
        categoryId: productForm.categoryId ? Number(productForm.categoryId) : null,
        specification: productForm.specification,
        sku: productForm.sku,
        stockQuantity,
        isActive: productForm.isActive,
        imageUrl: productForm.imageUrl,
        removeImage: productForm.removeImage,
        isFeatured: productForm.isFeatured,
        isPriceApproved: productForm.isPriceApproved,
        isStockConfirmed: productForm.isStockConfirmed,
        variants: productForm.variants.map((variant) => ({
          id: variant.id || null, name: variant.name.trim(), sku: variant.sku?.trim() || null,
          price: Number(variant.price), stockQuantity: Number(variant.stockQuantity),
          salePrice: variant.salePrice === '' || variant.salePrice == null ? null : Number(variant.salePrice),
          isActive: variant.isActive !== false, imageUrl: variant.imageUrl || null
        }))
      };

      if (editingProduct) {
        await apiClient.put(`/api/products/${editingProduct.id}`, payload);
        setStatusMessage('Product updated successfully.');
      } else {
        await apiClient.post('/api/products', payload);
        setStatusMessage('Product created successfully.');
      }

      setShowProductModal(false);
      loadData();
    } catch (err) {
      setError(err.response?.data?.message || 'Error saving product. Check the product fields and try again.');
    } finally {
      setSavingProduct(false);
    }
  };

  const createCategory = async (event) => {
    event.preventDefault();
    const name = newCategoryName.trim();
    if (name.length < 2) return;
    try {
      const response = await apiClient.post('/api/categories', { name, isActive: true });
      setCategories((current) => [...current, response.data].sort((a, b) => a.name.localeCompare(b.name)));
      setNewCategoryName('');
      setStatusMessage(`Category “${response.data.name}” added.`);
    } catch (err) {
      setError(err.response?.data?.message || 'Could not save the category.');
    }
  };

  const updateCategory = async (category, changes) => {
    try {
      const response = await apiClient.put(`/api/categories/${category.id}`, { name: changes.name ?? category.name, isActive: changes.isActive ?? category.isActive });
      setCategories((current) => current.map((item) => item.id === category.id ? response.data : item).sort((a, b) => a.name.localeCompare(b.name)));
      setStatusMessage('Category saved.');
    } catch (err) {
      setError(err.response?.data?.message || 'Could not update the category.');
    }
  };

  const uploadProductImage = async (file) => {
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp', 'image/gif'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      setError('Choose a JPEG, PNG, WebP, or GIF image under 5 MB.');
      return;
    }
    const form = new FormData();
    form.append('file', file);
    setUploadingImage(true);
    setError('');
    try {
      const response = await apiClient.post('/api/products/upload', form);
      setProductForm((previous) => ({ ...previous, imageUrl: response.data.url, removeImage: false }));
      setStatusMessage('Image uploaded. Save the product to persist its image URL.');
    } catch (err) {
      setError(err.response?.data?.message || 'Image upload failed. The existing product image was left unchanged.');
    } finally {
      setUploadingImage(false);
    }
  };

  const visibleProducts = products.filter((p) => `${p.name} ${p.brand || ''} ${p.sku || ''} ${p.category || ''}`.toLowerCase().includes(productSearch.toLowerCase()));
  const adminProductPrice = (product) => {
    const variants = (product.variants || []).filter((variant) => variant.isActive);
    const lowest = variants.length ? variants.reduce((current, candidate) => Number(candidate.salePrice ?? candidate.price) < Number(current.salePrice ?? current.price) ? candidate : current) : null;
    const regularPrice = Number(lowest?.price ?? product.price);
    const salePrice = lowest ? lowest.salePrice : product.salePrice;
    const currentPrice = Number(salePrice ?? regularPrice);
    const prefix = variants.length ? 'From ' : '';
    return salePrice != null && currentPrice < regularPrice
      ? `${prefix}R${currentPrice.toFixed(2)} (was R${regularPrice.toFixed(2)})`
      : `${prefix}R${currentPrice.toFixed(2)}`;
  };
  const visibleOrders = orders.filter((o) => {
    const query = orderSearch.toLowerCase();
    const matchesText = `${o.orderNumber} ${o.customerName} ${o.email} ${o.phone}`.toLowerCase().includes(query);
    const matchesDate = !orderDateFilter || new Date(o.orderDate).toISOString().slice(0, 10) === orderDateFilter;
    return matchesText && matchesDate && (orderPaymentFilter === 'All' || o.paymentStatus === orderPaymentFilter) && (orderStatusFilter === 'All' || o.status === orderStatusFilter);
  });

  if (authLoading) return <div className="admin-loading">Checking administrative privileges...</div>;

  return (
    <div className="admin-container">
      <div className="admin-header">
        <div>
          <h2>Belah Management Dashboard</h2>
          <p className="admin-subtext">Logged in as Administrator: <strong>{user?.username}</strong></p>
        </div>
        <div className="admin-tabs">
          <button
            className={`tab-btn ${activeTab === 'products' ? 'active' : ''}`}
            onClick={() => setActiveTab('products')}
          >
            Products & Inventory
          </button>
          <button
            className={`tab-btn ${activeTab === 'orders' ? 'active' : ''}`}
            onClick={() => setActiveTab('orders')}
          >
            Customer Orders
          </button>
          <button
            className={`tab-btn ${activeTab === 'reports' ? 'active' : ''}`}
            onClick={() => setActiveTab('reports')}
          >
            Reports
          </button>
        </div>
      </div>

      {statusMessage && <div className="admin-success-banner">{statusMessage}</div>}
      {error && <div className="admin-error-banner">{error}</div>}

      {/* PRODUCTS TAB */}
      {activeTab === 'products' && (
        <div className="admin-section">
          <div className="section-toolbar">
            <h3>Inventory Management ({products.length} items)</h3>
            <div className="section-actions"><input aria-label="Search products" placeholder="Search name, SKU, category" value={productSearch} onChange={(e) => setProductSearch(e.target.value)} /><button className="primary-action-btn" onClick={() => openProductModal()}>+ Add New Product</button></div>
          </div>

          <section className="category-management" aria-labelledby="category-heading">
            <h4 id="category-heading">Store categories</h4>
            <form onSubmit={createCategory} className="section-actions">
              <input aria-label="New category name" value={newCategoryName} onChange={(event) => setNewCategoryName(event.target.value)} placeholder="Add a category" maxLength="100" />
              <button type="submit" className="primary-action-btn">Add category</button>
            </form>
            <ul>{categories.map((category) => <li key={category.id}>
              <span>{category.name} {!category.isActive && <small>(hidden)</small>}</span>
              <button type="button" className="btn-edit" onClick={() => {
                const name = window.prompt('Category name', category.name);
                if (name?.trim()) updateCategory(category, { name: name.trim() });
              }}>Rename</button>
              <button type="button" className="btn-deactivate" onClick={() => updateCategory(category, { isActive: !category.isActive })}>{category.isActive ? 'Hide' : 'Show'}</button>
            </li>)}</ul>
          </section>

          {loading ? (
            <div className="admin-loading">Loading inventory...</div>
          ) : (
            <div className="table-responsive">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Image</th>
                    <th>Product Name</th>
                    <th>SKU</th>
                    <th>Price</th>
                    <th>Stock</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleProducts.map((p) => (
                    <tr key={p.id}>
                      <td>
                        <img
                          src={resolveProductImage(p.imageUrl)}
                          alt={p.name}
                          className="table-img"
                          onError={(e) => { e.currentTarget.onerror = null; e.currentTarget.src = '/images/product-placeholder.svg'; }}
                        />
                      </td>
                      <td>
                        <strong>{p.name}</strong>
                        <div className="subtext">{[p.brand, p.category].filter(Boolean).join(' · ')}</div>
                        {(!p.isPriceApproved || !p.isStockConfirmed) && <div className="subtext">{p.isPriceApproved ? 'Price approved' : 'Price approval needed'} · {p.isStockConfirmed ? 'Stock verified' : 'Stock unconfirmed'}</div>}
                      </td>
                      <td>{p.sku || '—'}</td>
                      <td>{adminProductPrice(p)}</td>
                      <td>
                        <input
                          type="number"
                          className="stock-inline-input"
                          defaultValue={p.stockQuantity}
                          disabled={Boolean(p.variants?.length)}
                          onBlur={(e) => {
                            if (parseInt(e.target.value, 10) !== p.stockQuantity) {
                              handleStockUpdate(p.id, e.target.value);
                            }
                          }}
                        />
                      </td>
                      <td>
                        <span className={`status-pill ${p.isActive ? 'active-pill' : 'inactive-pill'}`}>
                          {p.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td>
                        <div className="action-buttons">
                          <button className="btn-edit" onClick={() => openProductModal(p)}>Edit</button>
                          <button
                            className={p.isActive ? 'btn-deactivate' : 'btn-activate'}
                            onClick={() => handleToggleProductStatus(p.id, p.isActive)}
                          >
                            {p.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {visibleProducts.length === 0 && <tr><td colSpan="7">No matching products.</td></tr>}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* ORDERS TAB */}
      {activeTab === 'orders' && (
        <div className="admin-section">
          <div className="section-toolbar">
            <h3>Customer Orders ({orders.length} total)</h3>
            <div className="section-actions"><input aria-label="Search orders" placeholder="Order number or customer" value={orderSearch} onChange={(e) => setOrderSearch(e.target.value)} /><input aria-label="Filter orders by date" type="date" value={orderDateFilter} onChange={(e) => setOrderDateFilter(e.target.value)} /><select aria-label="Filter by payment status" value={orderPaymentFilter} onChange={(e) => setOrderPaymentFilter(e.target.value)}><option>All</option>{['Pending','Paid','Failed','Expired','RefundRequired'].map((s) => <option key={s}>{s}</option>)}</select><select aria-label="Filter by fulfillment status" value={orderStatusFilter} onChange={(e) => setOrderStatusFilter(e.target.value)}><option>All</option>{['Pending','Processing','Shipped','Completed','Cancelled','RefundRequired'].map((s) => <option key={s}>{s}</option>)}</select><button className="refresh-btn" onClick={loadData}>↻ Refresh Orders</button></div>
          </div>

          {loading ? (
            <div className="admin-loading">Loading customer orders...</div>
          ) : (
            <div className="table-responsive">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Order #</th>
                    <th>Date</th>
                    <th>Customer</th>
                    <th>Total</th>
                    <th>Payment</th>
                    <th>Fulfillment Status</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleOrders.map((o) => (
                    <tr key={o.orderId}>
                      <td>
                        <button type="button" className="order-detail-link" onClick={() => setSelectedOrder(o)}>
                        <strong>{o.orderNumber}</strong>
                        <div className="subtext">{o.items?.length || 0} items</div>
                        </button>
                      </td>
                      <td>{new Date(o.orderDate).toLocaleDateString()}</td>
                      <td>
                        <div>{o.customerName}</div>
                        <div className="subtext">{o.email} | {o.phone}</div>
                        <div className="subtext">{o.address}, {o.city}, {o.province} {o.postalCode}</div>
                      </td>
                      <td><strong>R{o.totalAmount.toFixed(2)}</strong></td>
                      <td>
                        <span className={`status-pill payment-${(o.paymentStatus || 'pending').toLowerCase()}`}>
                          {o.paymentStatus}
                        </span>
                      </td>
                      <td>
                        <select
                          className="status-select"
                          value={o.status}
                          onChange={(e) => handleOrderStatusUpdate(o.orderId, e.target.value)}
                        >
                          <option value="Pending">Pending</option>
                          <option value="Processing">Processing</option>
                          <option value="Shipped">Shipped</option>
                          <option value="Completed">Completed</option>
                          <option value="Cancelled">Cancelled</option>
                          <option value="RefundRequired">Refund required</option>
                        </select>
                      </td>
                    </tr>
                  ))}
                  {visibleOrders.length === 0 && <tr><td colSpan="6">No matching orders.</td></tr>}
                </tbody>
              </table>
              {orders.some((o) => o.paymentStatus === 'RefundRequired') && <p role="status" className="admin-error-banner">Refund required orders need manual handling. No refund is initiated automatically.</p>}
            </div>
          )}
        </div>
      )}

      {activeTab === 'reports' && (
        <div className="admin-section">
          <div className="section-toolbar">
            <div><h3>Inventory and sales reports</h3><p className="subtext">Sales include successfully verified, paid orders only.</p></div>
            <button className="refresh-btn" onClick={loadData}>↻ Refresh reports</button>
          </div>
          {loading ? <div className="admin-loading">Loading reports…</div> : inventoryReport && salesReport && (
            <>
              <div className="commerce-report-metrics">
                <article><span>Paid orders · 30 days</span><strong>{salesReport.paidOrders}</strong></article>
                <article><span>Revenue · ZAR</span><strong>R{Number(salesReport.revenue).toFixed(2)}</strong></article>
                <article><span>Average order · ZAR</span><strong>R{Number(salesReport.averageOrderValue).toFixed(2)}</strong></article>
                <article><span>Low stock · 5 or fewer</span><strong>{inventoryReport.lowStockRows}</strong></article>
                <article><span>Out of stock</span><strong>{inventoryReport.outOfStockRows}</strong></article>
                <article><span>Stock confirmation needed</span><strong>{inventoryReport.unconfirmedRows}</strong></article>
              </div>
              <section className="commerce-report-section">
                <h4>Low stock and availability</h4>
                <div className="table-responsive"><table className="admin-table">
                  <thead><tr><th>Product</th><th>SKU</th><th>Stock</th><th>Listing</th><th>Stock record</th></tr></thead>
                  <tbody>{inventoryReport.items.filter((item) => item.isActive && (item.isLowStock || !item.isStockConfirmed)).map((item) => (
                    <tr key={`${item.productId}-${item.variantId || 'base'}`}><td>{item.displayName}</td><td>{item.sku || '—'}</td><td>{item.stockQuantity}</td><td>{item.isActive ? 'Active' : 'Inactive'}</td><td>{item.isStockConfirmed ? 'Confirmed' : 'Needs confirmation'}</td></tr>
                  ))}{inventoryReport.items.filter((item) => item.isActive && (item.isLowStock || !item.isStockConfirmed)).length === 0 && <tr><td colSpan="5">No active products are low or awaiting stock confirmation.</td></tr>}</tbody>
                </table></div>
              </section>
              <section className="commerce-report-section">
                <h4>Best-selling products · 30 days</h4>
                <div className="table-responsive"><table className="admin-table">
                  <thead><tr><th>Product</th><th>Units sold</th><th>Line value · ZAR</th></tr></thead>
                  <tbody>{salesReport.topProducts.map((item) => <tr key={item.productName}><td>{item.productName}</td><td>{item.unitsSold}</td><td>R{Number(item.revenue).toFixed(2)}</td> </tr>)}{salesReport.topProducts.length === 0 && <tr><td colSpan="3">No paid product sales in this period.</td></tr>}</tbody>
                </table></div>
              </section>
            </>
          )}
        </div>
      )}

      {selectedOrder && (
        <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setSelectedOrder(null); }}>
          <section className="modal-card order-detail-modal" role="dialog" aria-modal="true" aria-labelledby="order-detail-title">
            <div className="order-detail-heading">
              <div><p className="admin-kicker">Order details</p><h3 id="order-detail-title">{selectedOrder.orderNumber}</h3></div>
              <button type="button" className="btn-cancel" onClick={() => setSelectedOrder(null)} aria-label="Close order details">Close</button>
            </div>
            <div className="order-detail-statuses">
              <p>Payment <strong>{selectedOrder.paymentStatus}</strong></p>
              <p>Fulfilment <strong>{selectedOrder.status}</strong></p>
              <p>Created <strong>{new Date(selectedOrder.orderDate).toLocaleString()}</strong></p>
              <p>Delivery fee <strong>R{Number(selectedOrder.deliveryFee || 0).toFixed(2)}</strong></p>
              <p>Total <strong>R{Number(selectedOrder.totalAmount).toFixed(2)}</strong></p>
            </div>
            <div className="order-detail-columns">
              <section><h4>Customer and delivery</h4><p>{selectedOrder.customerName}</p><p>{selectedOrder.email}</p><p>{selectedOrder.phone}</p><p>{selectedOrder.address}, {selectedOrder.city}, {selectedOrder.province} {selectedOrder.postalCode}</p></section>
        <section><h4>Items</h4><ul>{(selectedOrder.items || []).map((item) => <li key={item.id || `${item.productId}-${item.productName}`}><span>{item.productName} × {item.quantity}</span><strong>R{Number(item.subtotal).toFixed(2)}</strong>{(item.selectedVariantName || item.selectedColor || item.selectedStorage) && <small>{[item.selectedVariantName, item.selectedColor, item.selectedStorage].filter(Boolean).join(' · ')}</small>}</li>)}</ul></section>
            </div>
            {selectedOrder.paymentStatus === 'RefundRequired' && <p className="admin-error-banner" role="status">Refund follow-up is required. This dashboard does not issue or confirm refunds.</p>}
          </section>
        </div>
      )}

      {/* MODAL: ADD / EDIT PRODUCT */}
      {showProductModal && (
        <div className="modal-backdrop">
          <div className="modal-card">
            <h3>{editingProduct ? 'Edit Product' : 'Add New Product'}</h3>
            <form onSubmit={handleSaveProduct} className="modal-form">
              <div className="form-group">
                  <label htmlFor="product-name">Product Name</label>
                <input
                    id="product-name"
                  type="text"
                  required
                  value={productForm.name}
                  onChange={(e) => setProductForm({ ...productForm, name: e.target.value })}
                />
              </div>

              <div className="form-row">
                <div className="form-group"><label htmlFor="product-brand">Brand</label><input id="product-brand" maxLength="160" value={productForm.brand} onChange={(e) => setProductForm({ ...productForm, brand: e.target.value })} /></div>
                <div className="form-group"><label htmlFor="product-type">Type / concentration</label><input id="product-type" maxLength="120" placeholder="Only enter verified details" value={productForm.productType} onChange={(e) => setProductForm({ ...productForm, productType: e.target.value })} /></div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label>Price (ZAR)</label>
                  <input
                    type="number"
                    min="0"
                    max="1000000"
                    step="0.01"
                    required={productForm.variants.length === 0}
                    value={productForm.price}
                    onChange={(e) => setProductForm({ ...productForm, price: e.target.value })}
                    disabled={productForm.variants.length > 0}
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="product-sale-price">Sale Price (ZAR, optional)</label>
                  <input id="product-sale-price" type="number" min="0.01" max="1000000" step="0.01" value={productForm.salePrice} onChange={(e) => setProductForm({ ...productForm, salePrice: e.target.value })} disabled={productForm.variants.length > 0} placeholder="Lower than regular price" />
                  {productForm.variants.length > 0 && <small>Set sale prices per size or option below.</small>}
                </div>
                <div className="form-group">
                  <label>Stock Quantity</label>
                  <input
                    type="number"
                    min="0"
                    step="1"
                    required={productForm.variants.length === 0}
                    value={productForm.stockQuantity}
                    onChange={(e) => setProductForm({ ...productForm, stockQuantity: e.target.value })}
                    disabled={productForm.variants.length > 0}
                  />
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="product-sku">SKU</label>
                  <input
                    id="product-sku"
                    type="text"
                    value={productForm.sku}
                    onChange={(e) => setProductForm({ ...productForm, sku: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="product-category">Category</label>
                  <input
                    id="product-category"
                    type="text"
                    list="product-category-options"
                    required
                    value={productForm.category}
                    onChange={(e) => {
                      const category = categories.find((item) => item.name.toLowerCase() === e.target.value.trim().toLowerCase());
                      setProductForm({ ...productForm, category: e.target.value, categoryId: category ? String(category.id) : '' });
                    }}
                  />
                  <datalist id="product-category-options">{categories.filter((category) => category.isActive).map((category) => <option key={category.id} value={category.name} />)}</datalist>
                </div>
              </div>

              <section className="variant-editor" aria-labelledby="variant-editor-heading">
                <div className="section-toolbar"><h4 id="variant-editor-heading">Sizes or options</h4><button type="button" className="btn-edit" onClick={() => setProductForm((current) => ({ ...current, variants: [...current.variants, { name: '', sku: '', price: '', salePrice: '', stockQuantity: '', isActive: true, imageUrl: '' }] }))}>Add size / option</button></div>
                {productForm.variants.map((variant, index) => <div className="variant-editor-row" key={variant.id || `new-${index}`}>
                  <label>Size or option<input required maxLength="100" value={variant.name} onChange={(e) => setProductForm((current) => ({ ...current, variants: current.variants.map((item, itemIndex) => itemIndex === index ? { ...item, name: e.target.value } : item) }))} placeholder="For example, 50 ml" /></label>
                  <label>SKU<input maxLength="100" value={variant.sku || ''} onChange={(e) => setProductForm((current) => ({ ...current, variants: current.variants.map((item, itemIndex) => itemIndex === index ? { ...item, sku: e.target.value } : item) }))} /></label>
                  <label>Price (ZAR)<input required type="number" min="0" max="1000000" step="0.01" value={variant.price} onChange={(e) => setProductForm((current) => ({ ...current, variants: current.variants.map((item, itemIndex) => itemIndex === index ? { ...item, price: e.target.value } : item) }))} /></label>
                  <label>Sale price (optional)<input type="number" min="0.01" max="1000000" step="0.01" value={variant.salePrice || ''} onChange={(e) => setProductForm((current) => ({ ...current, variants: current.variants.map((item, itemIndex) => itemIndex === index ? { ...item, salePrice: e.target.value } : item) }))} /></label>
                  <label>Stock<input required type="number" min="0" step="1" value={variant.stockQuantity} onChange={(e) => setProductForm((current) => ({ ...current, variants: current.variants.map((item, itemIndex) => itemIndex === index ? { ...item, stockQuantity: e.target.value } : item) }))} /></label>
                  <label className="variant-active"><input type="checkbox" checked={variant.isActive !== false} onChange={(e) => setProductForm((current) => ({ ...current, variants: current.variants.map((item, itemIndex) => itemIndex === index ? { ...item, isActive: e.target.checked } : item) }))} /> Available</label>
                  <button type="button" className="btn-deactivate" onClick={() => setProductForm((current) => ({ ...current, variants: current.variants.filter((_, itemIndex) => itemIndex !== index) }))}>Remove</button>
                </div>)}
              </section>

              <div className="form-group">
                <label htmlFor="product-image-url">Image URL or upload</label>
                <input
                  id="product-image-url"
                  type="text"
                  placeholder="https://... or upload an image"
                  required={!editingProduct}
                  value={productForm.imageUrl}
                  onChange={(e) => setProductForm({ ...productForm, imageUrl: e.target.value, removeImage: false })}
                />
                <input type="file" accept="image/jpeg,image/png,image/webp,image/gif" aria-label="Upload product image" onChange={(e) => uploadProductImage(e.target.files?.[0])} disabled={uploadingImage} />
                {editingProduct && <button type="button" className="btn-deactivate" onClick={() => setProductForm({ ...productForm, imageUrl: '', removeImage: true })}>Remove image and use placeholder</button>}
                {uploadingImage && <span role="status">Uploading image…</span>}
              </div>

              <div className="form-group">
                <label htmlFor="product-description">Description</label>
                <textarea
                  id="product-description"
                  rows="3"
                  required
                  value={productForm.description}
                  onChange={(e) => setProductForm({ ...productForm, description: e.target.value })}
                />
              </div>

              <div className="form-group checkbox-group">
                <label>
                  <input
                    type="checkbox"
                    checked={productForm.isActive}
                    onChange={(e) => setProductForm({ ...productForm, isActive: e.target.checked })}
                  />
                  Product Active (Visible in Storefront)
                </label>
                <label><input type="checkbox" checked={productForm.isFeatured} onChange={(e) => setProductForm({ ...productForm, isFeatured: e.target.checked })} /> Feature on the home page</label>
                <label><input type="checkbox" checked={productForm.isPriceApproved} onChange={(e) => setProductForm({ ...productForm, isPriceApproved: e.target.checked })} /> Price approved for sale</label>
                <label><input type="checkbox" checked={productForm.isStockConfirmed} onChange={(e) => setProductForm({ ...productForm, isStockConfirmed: e.target.checked })} /> Physical stock verified</label>
              </div>

              <div className="modal-actions">
                <button type="button" className="btn-cancel" onClick={() => setShowProductModal(false)}>Cancel</button>
                <button type="submit" className="btn-save" disabled={savingProduct || uploadingImage}>{savingProduct ? 'Saving…' : editingProduct ? 'Save Changes' : 'Create Product'}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminDashboard;
