import React, { useState } from 'react';
import { 
    Container, 
    TextField, 
    Button, 
    Typography, 
    Box,
    FormControlLabel,
    Checkbox
} from '@mui/material';
import apiClient from '../config/apiClient';

const PostProduct = () => {
    const [product, setProduct] = useState({
        name: '',
        description: '',
        price: '',
        imageUrl: '',
        category: '',
        specification: '',
        sku: '',
        stockQuantity: 0,
        isActive: true
    });
    const [imageFile, setImageFile] = useState(null); // Handle file uploads
    const [isSubmitting, setIsSubmitting] = useState(false); // Submission state

    const handleChange = (e) => {
        const { name, value, type, checked } = e.target;
        setProduct((prevProduct) => ({
            ...prevProduct,
            [name]: type === 'checkbox' ? checked : value,
        }));
    };

    const handleFileChange = (e) => {
        setImageFile(e.target.files[0]); // Update the selected file
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        const priceNum = parseFloat(product.price);
        const stockNum = parseInt(product.stockQuantity, 10);

        if (isNaN(priceNum) || priceNum < 0) {
            alert('Price must be a valid non-negative number.');
            return;
        }

        if (isNaN(stockNum) || stockNum < 0) {
            alert('Stock quantity must be a non-negative integer.');
            return;
        }

        setIsSubmitting(true);

        try {
            let imageUrl = product.imageUrl;

            // Upload image if a file is selected
            if (imageFile) {
                const formData = new FormData();
                formData.append('file', imageFile);

                const uploadResponse = await apiClient.post('/api/products/upload', formData, {
                    headers: {
                        'Content-Type': 'multipart/form-data'
                    }
                });

                imageUrl = uploadResponse.data.url; // Retrieve uploaded image URL
            }

            // Submit product details
            const response = await apiClient.post('/api/products', {
                ...product,
                price: priceNum,
                stockQuantity: stockNum,
                imageUrl
            });

            alert('Product created successfully!');
            console.log(response.data);
            // Reset form after successful submission
            setProduct({
                name: '',
                description: '',
                price: '',
                imageUrl: '',
                category: '',
                specification: '',
                sku: '',
                stockQuantity: 0,
                isActive: true
            });
            setImageFile(null);
        } catch (error) {
            console.error('There was an error creating the product!', error);
            const serverMessage = error.response?.data?.message || 'Failed to create the product. Please try again.';
            alert(serverMessage);
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <Container maxWidth="sm" style={{ marginTop: '50px' }}>
            <Typography variant="h4" component="h1" gutterBottom>
                Create a New Product
            </Typography>
            <Box 
                component="form" 
                onSubmit={handleSubmit} 
                sx={{ display: 'flex', flexDirection: 'column', gap: '20px', paddingBottom: '20px' }}
            >
                <TextField
                    label="Name"
                    name="name"
                    value={product.name}
                    onChange={handleChange}
                    required
                    fullWidth
                />
                <TextField
                    label="SKU (Stock Keeping Unit)"
                    name="sku"
                    value={product.sku}
                    onChange={handleChange}
                    fullWidth
                    placeholder="e.g. IPHONE-13-128-BLK"
                />
                <TextField
                    label="Stock Quantity"
                    name="stockQuantity"
                    type="number"
                    value={product.stockQuantity}
                    onChange={handleChange}
                    required
                    fullWidth
                    inputProps={{ min: 0 }}
                />
                <TextField
                    label="Description"
                    name="description"
                    value={product.description}
                    onChange={handleChange}
                    required
                    fullWidth
                    multiline
                    rows={4}
                />
                <TextField
                    label="Price (ZAR)"
                    name="price"
                    type="number"
                    value={product.price}
                    onChange={handleChange}
                    required
                    fullWidth
                    inputProps={{ min: 0, step: "0.01" }}
                />
                {/* File input for image upload */}
                <div>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>
                        Product Image:
                    </label>
                    <input
                        type="file"
                        accept="image/*"
                        onChange={handleFileChange}
                        style={{ marginBottom: '10px' }}
                    />
                </div>
                <TextField
                    label="Category"
                    name="category"
                    value={product.category}
                    onChange={handleChange}
                    required
                    fullWidth
                />
                <TextField
                    label="Specification"
                    name="specification"
                    value={product.specification}
                    onChange={handleChange}
                    required
                    fullWidth
                    multiline
                    rows={4}
                />
                <FormControlLabel
                    control={
                        <Checkbox
                            name="isActive"
                            checked={product.isActive}
                            onChange={handleChange}
                            color="primary"
                        />
                    }
                    label="Active (Visible in store)"
                />
                <Button 
                    type="submit" 
                    variant="contained" 
                    color="primary"
                    disabled={isSubmitting} // Disable button during submission
                    sx={{ alignSelf: 'center', width: '150px', marginTop: '10px' }}
                >
                    {isSubmitting ? 'Submitting...' : 'Submit'}
                </Button>
            </Box>
        </Container>
    );
};

export default PostProduct;
