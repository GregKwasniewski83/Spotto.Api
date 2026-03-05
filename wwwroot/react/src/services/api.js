import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:7001/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor for error handling
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const token = localStorage.getItem('token');
      // Only redirect to login if user has a token (not anonymous)
      if (token) {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        window.location.href = '/login';
      }
      // For anonymous users, just let the error pass through
    }
    return Promise.reject(error);
  }
);

export const authAPI = {
  login: (credentials) => api.post('/auth/signin', credentials),
  register: (userData) => api.post('/auth/signup', userData),
  refresh: (refreshToken) => api.post('/auth/refresh', { refreshToken }),
};

export const agentAPI = {
  // Get business profiles that user is agent for
  getMyBusinessProfiles: () => api.get('/agent/my-business'),
  
  // Get agents for specific business
  getBusinessAgents: (businessProfileId) => 
    api.get(`/agent/business-profile/${businessProfileId}`),
  
  // Remove agent
  removeAgent: (businessProfileId, agentUserId) => 
    api.delete(`/agent/business-profile/${businessProfileId}/agent/${agentUserId}`),
  
  // Get pending invitations
  getPendingInvitations: (businessProfileId) => 
    api.get(`/agent/invitations/business-profile/${businessProfileId}`),
  
  // Cancel invitation
  cancelInvitation: (invitationId) => 
    api.delete(`/agent/invitations/${invitationId}`),
};

export const facilityAPI = {
  // Get facilities for business profile
  getFacilitiesByBusiness: (businessProfileId) => 
    api.get(`/facility/business/${businessProfileId}`),
  
  // Get facility details
  getFacility: (facilityId) => api.get(`/facility/${facilityId}`),
  
  // Get time slots for facility and date
  getTimeSlots: (facilityId, date) => 
    api.get(`/facility/${facilityId}/timeslots?date=${date}`),
};

export const reservationAPI = {
  // Get reservations for facility and date range
  getReservations: (facilityId, startDate, endDate) => 
    api.get(`/reservation?facilityId=${facilityId}&startDate=${startDate}&endDate=${endDate}`),
  
  // Create reservation (for agents)
  createReservation: (reservationData) => 
    api.post('/reservation', reservationData),
  
  // Update reservation
  updateReservation: (reservationId, updateData) => 
    api.put(`/reservation/${reservationId}`, updateData),
  
  // Cancel reservation
  cancelReservation: (reservationId) => 
    api.delete(`/reservation/${reservationId}`),
  
  // Mark reservation as paid
  markAsPaid: (reservationId) => 
    api.patch(`/reservation/${reservationId}/mark-paid`),
  
  // Reschedule reservation
  reschedule: (reservationId, rescheduleData) => 
    api.patch(`/reservation/${reservationId}/reschedule`, rescheduleData),
};

export const businessAPI = {
  // Get business profile details
  getBusinessProfile: (businessProfileId) => 
    api.get(`/businessprofile/${businessProfileId}`),
};

export default api;