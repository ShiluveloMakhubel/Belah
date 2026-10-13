import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import App from './App';
import AdminDashboard from './pages/AdminDashboard';
import ProductsPage from './pages/ProductsPage';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from './config/AuthContext';

jest.mock('./config/apiClient', () => ({
  __esModule: true,
  default: { get: jest.fn().mockResolvedValue({ data: [] }), patch: jest.fn().mockResolvedValue({ data: {} }) },
}));

test('renders the storefront navigation', () => {
  render(<App />);
  const linkElement = screen.getByRole('link', { name: 'Shop', exact: true });
  expect(linkElement).toBeInTheDocument();
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
  await screen.findByText('Customer Orders');
  fireEvent.click(screen.getByRole('button', { name: /customer orders/i }));
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
