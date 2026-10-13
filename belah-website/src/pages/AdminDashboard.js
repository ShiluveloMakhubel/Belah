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
  const [orders, setOrders] = useState([]);
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
    description: '',
    price: '',
    category: '',
    specification: '',
    sku: '',
    stockQuantity: 0,
    isActive: true,
    imageUrl: ''
  });

  const loadData = useCallback(async () => {
    setLoading(true);
    setError('');
    setStatusMessage('');
    try {
      if (activeTab === 'products') {
        const res = await apiClient.get('/api/products?includeInactive=true');
        setProducts(res.data);
      } else {
        const res = await apiClient.get('/api/orders');
        setOrders(res.data);
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
        description: product.description,
        price: product.price,
        category: product.category || '',
        specification: product.specification || '',
        sku: product.sku || '',
        stockQuantity: product.stockQuantity,
        isActive: product.isActive,
        imageUrl: product.imageUrl || ''
      });
    } else {
      setEditingProduct(null);
      setProductForm({
        name: '',
        description: '',
        price: '',
        category: '',
        specification: '',
        sku: '',
        stockQuantity: 0,
        isActive: true,
        imageUrl: ''
      });
    }
    setShowProductModal(true);
  };

  const handleSaveProduct = async (e) => {
    e.preventDefault();
    const price = Number(productForm.price);
    const stockQuantity = Number(productForm.stockQuantity);
    if (!productForm.name.trim() || !productForm.description.trim() || !productForm.category.trim() || !Number.isFinite(price) || price < 0 || price > 1000000 || Math.round(price * 100) !== price * 100 || !Number.isInteger(stockQuantity) || stockQuantity < 0) {
      setError('Enter a name, description, category, valid price, and whole non-negative stock quantity.');
      return;
    }
    setSavingProduct(true);
    setError('');
    try {
      const payload = {
        name: productForm.name.trim(),
        description: productForm.description.trim(),
        price,
        category: productForm.category.trim(),
        specification: productForm.specification,
        sku: productForm.sku,
        stockQuantity,
        isActive: productForm.isActive,
        imageUrl: productForm.imageUrl
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
      setProductForm((previous) => ({ ...previous, imageUrl: response.data.url }));
      setStatusMessage('Image uploaded. Save the product to persist its image URL.');
    } catch (err) {
      setError(err.response?.data?.message || 'Image upload failed. The existing product image was left unchanged.');
    } finally {
      setUploadingImage(false);
    }
  };

  const visibleProducts = products.filter((p) => `${p.name} ${p.sku || ''} ${p.category || ''}`.toLowerCase().includes(productSearch.toLowerCase()));
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
                        <div className="subtext">{p.category}</div>
                      </td>
                      <td>{p.sku || '—'}</td>
                      <td>R{p.price.toFixed(2)}</td>
                      <td>
                        <input
                          type="number"
                          className="stock-inline-input"
                          defaultValue={p.stockQuantity}
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
              <p>Total <strong>R{Number(selectedOrder.totalAmount).toFixed(2)}</strong></p>
            </div>
            <div className="order-detail-columns">
              <section><h4>Customer and delivery</h4><p>{selectedOrder.customerName}</p><p>{selectedOrder.email}</p><p>{selectedOrder.phone}</p><p>{selectedOrder.address}, {selectedOrder.city}, {selectedOrder.province} {selectedOrder.postalCode}</p></section>
              <section><h4>Items</h4><ul>{(selectedOrder.items || []).map((item) => <li key={item.id || `${item.productId}-${item.productName}`}><span>{item.productName} × {item.quantity}</span><strong>R{Number(item.subtotal).toFixed(2)}</strong>{(item.selectedColor || item.selectedStorage) && <small>{[item.selectedColor, item.selectedStorage].filter(Boolean).join(' · ')}</small>}</li>)}</ul></section>
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
                <div className="form-group">
                  <label>Price (ZAR)</label>
                  <input
                    type="number"
                    min="0"
                    max="1000000"
                    step="0.01"
                    required
                    value={productForm.price}
                    onChange={(e) => setProductForm({ ...productForm, price: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label>Stock Quantity</label>
                  <input
                    type="number"
                    min="0"
                    step="1"
                    required
                    value={productForm.stockQuantity}
                    onChange={(e) => setProductForm({ ...productForm, stockQuantity: e.target.value })}
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
                    onChange={(e) => setProductForm({ ...productForm, category: e.target.value })}
                  />
                  <datalist id="product-category-options"><option value="Wigs" /><option value="Frontals" /><option value="Closures" /><option value="Bundles" /><option value="Extensions" /><option value="Accessories" /></datalist>
                </div>
              </div>

              <div className="form-group">
                <label htmlFor="product-image-url">Image URL or upload</label>
                <input
                  id="product-image-url"
                  type="text"
                  placeholder="https://... or upload an image"
                  required={!editingProduct}
                  value={productForm.imageUrl}
                  onChange={(e) => setProductForm({ ...productForm, imageUrl: e.target.value })}
                />
                <input type="file" accept="image/jpeg,image/png,image/webp,image/gif" aria-label="Upload product image" onChange={(e) => uploadProductImage(e.target.files?.[0])} disabled={uploadingImage} />
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
