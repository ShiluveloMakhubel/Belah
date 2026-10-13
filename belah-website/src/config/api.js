export const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5037';

export const resolveProductImage = (url) => {
  if (!url) return '/images/product-placeholder.svg';
  if (url.startsWith('/images/')) return url;
  if (url.startsWith('/')) return `${API_BASE_URL.replace(/\/+$/, '')}${url}`;
  return url;
};

export default API_BASE_URL;
