import React from 'react';
import './Deals.css';
import { useNavigate } from 'react-router-dom';

const dealsData = [
  { id: 1, name: 'IPhone 13', price: 'R5,499', image: '/images/iphone13.png' },
  { id: 2, name: 'Discounted iPhone 11', price: 'R3,999', image: '/images/iphone11.png' },
  { id: 3, name: 'Stylish iphone 16', price: 'R1,299', image: '/images/iphone16.png' },
];


const Deals = () => {
   const navigate = useNavigate();
  return (
    <div className="deals-page">
      <h2 className="deals-title">🎉 Hot Exclusive Deals Just for You! 🎉</h2>
      <p className="deals-subtitle">Hurry! Limited time only 🔥</p>
      <div className="deals-grid">
        {dealsData.map(deal => (
          <div key={deal.id} className="deal-card animate-pop">
            <img src={deal.image} alt={deal.name} className="deal-image" />
            <div className="deal-info">
              <h3>{deal.name}</h3>
              <p className="deal-price">{deal.price}</p>
              <button className="shop-now" onClick={() => navigate('/products')} >Shop Now</button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default Deals;
