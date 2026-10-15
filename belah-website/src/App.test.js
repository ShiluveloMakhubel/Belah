import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import App from './App';
import AdminDashboard from './pages/AdminDashboard';
import ProductsPage from './pages/ProductsPage';
import ProductDetails from './pages/ProductDetails';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './config/AuthContext';

jest.mock('./config/apiClient', () => ({
  __esModule: true,
  default: { get: jest.fn().mockResolvedValue({ data: [] }), patch: jest.fn().mockResolvedValue({ data: {} }) },
}));

test('renders the storefront navigation', async () => {
  global.fetch = jest.fn().mockResolvedValue({ ok: true, json: async () => [] });
  render(<App />);
  const linkElement = screen.getByRole('link', { name: 'Shop', exact: true });
  expect(linkElement).toBeInTheDocument();
  expect(await screen.findByText('The collection is currently unavailable. Please try again later.')).toBeInTheDocument();
});

test('admin can inspect orders and the payment state stays read-only', async () => {
  const order = {
    orderId: 8, orderNumber: 'BELAH-TEST-8', orderDate: '2026-10-10T12:00:00Z',
    customerName: 'Test Customer', email: 'test@example.invalid', phone: '0000',
    address: '1 Test Road', city: 'Cape Town', province: 'Western Cape', postalCode: '8000',
    totalAmount: 550, paymentStatus: 'Paid', status: 'Processing',
    items: [{ id: 1, productName: 'Test Wig', quantity: 1, subtotal: 550 }],
  };
  const patch = jest.fn().mockResolvedValue({ data: { ...order, status: 'Shipped' } });
  const apiClient = require('./config/apiClient').default;
  apiClient.get.mockImplementation((url) => Promise.resolve({ data: url === '/api/orders' ? [order] : [] }));
  apiClient.patch.mockImplementation(patch);
  localStorage.setItem('belah_token', 'test-token');
  localStorage.setItem('belah_user', JSON.stringify({ username: 'admin', role: 'Admin' }));
  render(<MemoryRouter><AuthProvider><AdminDashboard /></AuthProvider></MemoryRouter>);
  await screen.findByText('Inventory Management (0 items)');
  fireEvent.click(screen.getByRole('button', { name: /customer orders/i }));
  await screen.findByText('Customer Orders (1 total)');
  fireEvent.click(await screen.findByRole('button', { name: /BELAH-TEST-8/i }));
  expect(screen.getByRole('dialog', { name: /BELAH-TEST-8/i })).toHaveTextContent('Test Wig');
  expect(screen.getByRole('dialog')).toHaveTextContent('test@example.invalid');
  expect(screen.getByRole('dialog')).toHaveTextContent('Payment Paid');

  const fulfillmentSelect = screen.getByDisplayValue('Processing');
  fireEvent.change(fulfillmentSelect, { target: { value: 'Shipped' } });
  await waitFor(() => expect(patch).toHaveBeenCalledWith('/api/orders/8/status', { status: 'Shipped' }));
  expect(await screen.findByText('Order #8 status updated to Shipped.')).toBeInTheDocument();
  expect(patch.mock.calls[0][1]).not.toHaveProperty('paymentStatus');
  localStorage.clear();
});

test('catalogue renders active API products and stock status', async () => {
  global.fetch = jest.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 42, name: 'API Wig', description: 'Saved in SQL', category: 'Wigs', price: 1250, stockQuantity: 3, isActive: true, imageUrl: '/images/product-placeholder.svg' }] });
  render(<MemoryRouter><ProductsPage /></MemoryRouter>);
  expect(await screen.findByRole('heading', { name: 'API Wig' })).toBeInTheDocument();
  expect(screen.getByText('In Stock (3 available)')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'View Details' })).toHaveAttribute('href', '/product/42');
});

test('all ten fragrance intake cards use the local image and recover from image load failures', async () => {
  const names = [
    'Oud Stallion Cologne', 'Parfums de Marly Paris', 'Amouage', 'Bvlgari Aqva Pour Homme',
    'Bvlgari Omnia', 'Pink Sapphire', 'Maison Margiela Paris', 'Valentina Eau de Parfum',
    'Sì Passione Eau de Parfum', 'Coach New York Eau de Parfum',
  ];
  const products = names.map((name, index) => ({
    id: index + 1, name, category: 'Fragrances', price: 0, stockQuantity: 0,
    isActive: true, isPriceApproved: false, isStockConfirmed: false,
    imageUrl: '/images/product-placeholder.svg',
  }));
  global.fetch = jest.fn().mockImplementation(async (url) => ({
    ok: true,
    json: async () => url.endsWith('/api/categories') ? [] : products,
  }));

  render(<MemoryRouter><ProductsPage /></MemoryRouter>);
  for (const name of names) {
    const image = await screen.findByRole('img', { name });
    expect(image).toHaveAttribute('src', '/images/product-placeholder.svg');
    fireEvent.error(image);
    expect(image).toHaveAttribute('src', '/images/product-placeholder.svg');
  }
});

test('product detail image falls back to the local placeholder after a load failure', async () => {
  global.fetch = jest.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ id: 8, name: 'Valentina Eau de Parfum', category: 'Fragrances', price: 0, stockQuantity: 0, isPriceApproved: false, isStockConfirmed: false, imageUrl: '/uploads/missing-valentina.jpg' }),
  });
  render(<MemoryRouter initialEntries={['/product/8']}><Routes><Route path="/product/:productId" element={<ProductDetails />} /></Routes></MemoryRouter>);
  const image = await screen.findByRole('img', { name: 'Valentina Eau de Parfum' });
  expect(image).toHaveAttribute('src', 'http://localhost:5037/uploads/missing-valentina.jpg');
  fireEvent.error(image);
  expect(image).toHaveAttribute('src', '/images/product-placeholder.svg');
});

test('catalogue supports brand search, category filters, and variant price ranges', async () => {
  global.fetch = jest.fn().mockImplementation(async (url) => ({
    ok: true,
    json: async () => url.endsWith('/api/categories')
      ? [{ id: 1, name: 'Fragrances', isActive: true }]
      : [{ id: 7, name: 'Sì Passione', brand: 'Giorgio Armani', description: 'Fragrance', category: 'Fragrances', price: 90, stockQuantity: 5, isActive: true, variants: [{ id: 21, name: '50 ml', price: 90, stockQuantity: 2, isActive: true }, { id: 22, name: '100 ml', price: 140, stockQuantity: 3, isActive: true }] }],
  }));
  render(<MemoryRouter><ProductsPage /></MemoryRouter>);
  expect(await screen.findByText('From R90.00')).toBeInTheDocument();
  expect(screen.getByText('In Stock (5 available)')).toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('Search products'), { target: { value: 'Armani' } });
  expect(screen.getByRole('heading', { name: 'Sì Passione' })).toBeInTheDocument();
});

test('catalogue labels indicative pricing and unconfirmed stock, and detail cannot add it to cart', async () => {
  global.fetch = jest.fn().mockImplementation(async (url) => ({
    ok: true,
    json: async () => url.includes('/api/products/')
      ? { id: 91, name: 'Oud Stallion Cologne', brand: 'Maison Crivelli', description: 'Intake listing', category: 'Fragrances', price: 4999, stockQuantity: 0, isActive: true, isPriceApproved: false, isStockConfirmed: false, imageUrl: '/images/product-placeholder.svg' }
      : url.endsWith('/api/categories') ? [{ id: 1, name: 'Fragrances', isActive: true }]
        : [{ id: 91, name: 'Oud Stallion Cologne', brand: 'Maison Crivelli', description: 'Intake listing', category: 'Fragrances', price: 4999, stockQuantity: 0, isActive: true, isPriceApproved: false, isStockConfirmed: false, imageUrl: '/images/product-placeholder.svg' }],
  }));
  render(<MemoryRouter><ProductsPage /></MemoryRouter>);
  expect(await screen.findByText('Indicative · R4999.00')).toBeInTheDocument();
  expect(screen.getAllByText('Availability unconfirmed').length).toBeGreaterThan(0);

  render(<MemoryRouter initialEntries={['/product/91']}><Routes><Route path="/product/:productId" element={<ProductDetails />} /></Routes></MemoryRouter>);
  expect(await screen.findByRole('button', { name: 'Price pending approval' })).toBeDisabled();
  expect(screen.getAllByText('Availability unconfirmed').length).toBeGreaterThan(0);
});

test('customer selects a size before adding the matching variant to the cart', async () => {
  localStorage.removeItem('cart');
  global.fetch = jest.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ id: 9, name: 'Fragrance', brand: 'Example House', description: 'Test listing', category: 'Fragrances', price: 0, stockQuantity: 5, isActive: true, variants: [{ id: 31, name: '50 ml', price: 95, stockQuantity: 3, isActive: true }, { id: 32, name: '100 ml', price: 150, stockQuantity: 2, isActive: true }] }),
  });
  render(<MemoryRouter initialEntries={['/product/9']}><Routes><Route path="/product/:productId" element={<ProductDetails />} /></Routes></MemoryRouter>);
  fireEvent.change(await screen.findByLabelText('Select product size or option'), { target: { value: '31' } });
  fireEvent.click(await screen.findByRole('button', { name: /add to cart/i }));
  const cart = JSON.parse(localStorage.getItem('cart'));
  expect(cart).toHaveLength(1);
  expect(cart[0]).toMatchObject({ variantId: 31, selectedVariantName: '50 ml', price: 95, stockQuantity: 3 });
});
