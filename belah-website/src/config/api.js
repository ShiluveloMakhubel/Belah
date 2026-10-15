const configuredApiUrl = process.env.REACT_APP_API_URL?.trim();

if (process.env.NODE_ENV === 'production' && !configuredApiUrl) {
  throw new Error('REACT_APP_API_URL must be set for production builds.');
}

export const API_BASE_URL = (configuredApiUrl || 'http://localhost:5037').replace(/\/+$/, '');

export const resolveProductImage = (url) => {
  if (!url) return '/images/product-placeholder.svg';
  if (url.startsWith('/images/')) return url;
  if (url.startsWith('/')) return `${API_BASE_URL.replace(/\/+$/, '')}${url}`;
  return url;
};

export default API_BASE_URL;
