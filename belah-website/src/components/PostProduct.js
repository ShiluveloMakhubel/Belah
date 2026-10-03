import React, { useState } from 'react';
import axios from 'axios';
import { 
    Container, 
    TextField, 
    Button, 
    Typography, 
    Box 
} from '@mui/material';

const PostProduct = () => {
    const [product, setProduct] = useState({
        name: '',
        description: '',
        price: '',
        imageUrl: '',
        category: '',
        specification: ''
    });
    const [imageFile, setImageFile] = useState(null); // Handle file uploads
    const [isSubmitting, setIsSubmitting] = useState(false); // Submission state

    const handleChange = (e) => {
        const { name, value } = e.target;
        setProduct((prevProduct) => ({
            ...prevProduct,
            [name]: value,
        }));
    };

    const handleFileChange = (e) => {
        setImageFile(e.target.files[0]); // Update the selected file
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setIsSubmitting(true);

        try {
            let imageUrl = product.imageUrl;

            // Upload image if a file is selected
            if (imageFile) {
                const formData = new FormData();
                formData.append('file', imageFile);

                const uploadResponse = await axios.post('http://localhost:5037/api/products/upload', formData, {
                    headers: {
                        'Content-Type': 'multipart/form-data'
                    }
                });

                imageUrl = uploadResponse.data.url; // Retrieve uploaded image URL
            }

            // Submit product details
            const response = await axios.post('http://localhost:5037/api/products', {
                ...product,
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
                specification: ''
            });
            setImageFile(null);
        } catch (error) {
            console.error('There was an error creating the product!', error);
            alert('Failed to create the product. Please try again.');
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
                    label="Price"
                    name="price"
                    type="number"
                    value={product.price}
                    onChange={handleChange}
                    required
                    fullWidth
                />
                {/* File input for image upload */}
                <input 
                    type="file" 
                    accept="image/*" 
                    onChange={handleFileChange}
                    style={{ marginBottom: '20px' }} 
                />
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
                <Button 
                    type="submit" 
                    variant="contained" 
                    color="primary"
                    disabled={isSubmitting} // Disable button during submission
                    sx={{ alignSelf: 'center', width: '150px', marginTop: '20px' }}
                >
                    {isSubmitting ? 'Submitting...' : 'Submit'}
                </Button>
            </Box>
        </Container>
    );
};

export default PostProduct;
