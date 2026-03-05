import React, { useState, useEffect } from 'react';
import { agentAPI, facilityAPI, reservationAPI, businessAPI } from '../services/api';
import FacilitySelector from '../components/FacilitySelector';
import ScheduleView from '../components/ScheduleView';
import ReservationModal from '../components/ReservationModal';
import { Calendar, Building2, Users, Clock, DollarSign, AlertCircle } from 'lucide-react';
import { format, addDays, startOfWeek } from 'date-fns';

function AgentDashboard() {
  const [businessProfiles, setBusinessProfiles] = useState([]);
  const [selectedBusiness, setSelectedBusiness] = useState(null);
  const [facilities, setFacilities] = useState([]);
  const [selectedFacility, setSelectedFacility] = useState(null);
  const [currentDate, setCurrentDate] = useState(new Date());
  const [timeSlots, setTimeSlots] = useState([]);
  const [reservations, setReservations] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [showReservationModal, setShowReservationModal] = useState(false);
  const [selectedTimeSlot, setSelectedTimeSlot] = useState(null);

  useEffect(() => {
    loadBusinessProfiles();
  }, []);

  useEffect(() => {
    if (selectedBusiness) {
      loadFacilities(selectedBusiness.businessProfileId);
    }
  }, [selectedBusiness]);

  useEffect(() => {
    if (selectedFacility && currentDate) {
      loadTimeSlots();
      loadReservations();
    }
  }, [selectedFacility, currentDate]);

  const loadBusinessProfiles = async () => {
    try {
      setIsLoading(true);
      const response = await agentAPI.getMyBusinessProfiles();
      const profiles = response.data.agents || [];
      
      // Get business details for each profile
      const profilesWithDetails = await Promise.all(
        profiles.map(async (agent) => {
          try {
            const businessResponse = await businessAPI.getBusinessProfile(agent.businessProfileId);
            return {
              ...agent,
              businessDetails: businessResponse.data
            };
          } catch (error) {
            console.error(`Failed to load business details for ${agent.businessProfileId}:`, error);
            return agent;
          }
        })
      );

      setBusinessProfiles(profilesWithDetails);
      
      if (profilesWithDetails.length > 0) {
        setSelectedBusiness(profilesWithDetails[0]);
      }
    } catch (error) {
      console.error('Error loading business profiles:', error);
      if (error.response?.status === 401) {
        // For anonymous users, show demo message instead of error
        setError('Tryb demonstracyjny - zaloguj się aby zobaczyć rzeczywiste dane');
        setBusinessProfiles([]);
      } else {
        setError('Nie udało się załadować profili biznesowych');
      }
    } finally {
      setIsLoading(false);
    }
  };

  const loadFacilities = async (businessProfileId) => {
    try {
      const response = await facilityAPI.getFacilitiesByBusiness(businessProfileId);
      const facilityList = response.data || [];
      setFacilities(facilityList);
      
      if (facilityList.length > 0) {
        setSelectedFacility(facilityList[0]);
      }
    } catch (error) {
      console.error('Error loading facilities:', error);
      setError('Nie udało się załadować obiektów');
    }
  };

  const loadTimeSlots = async () => {
    try {
      const dateStr = format(currentDate, 'yyyy-MM-dd');
      const response = await facilityAPI.getTimeSlots(selectedFacility.id, dateStr);
      setTimeSlots(response.data || []);
    } catch (error) {
      console.error('Error loading time slots:', error);
      setTimeSlots([]);
    }
  };

  const loadReservations = async () => {
    try {
      const startDate = format(startOfWeek(currentDate, { weekStartsOn: 1 }), 'yyyy-MM-dd');
      const endDate = format(addDays(currentDate, 6), 'yyyy-MM-dd');
      
      const response = await reservationAPI.getReservations(
        selectedFacility.id,
        startDate,
        endDate
      );
      setReservations(response.data || []);
    } catch (error) {
      console.error('Error loading reservations:', error);
      setReservations([]);
    }
  };

  const handleTimeSlotClick = (timeSlot) => {
    // Check if time slot is available
    const existingReservation = reservations.find(r => 
      r.timeSlotId === timeSlot.id && 
      format(new Date(r.date), 'yyyy-MM-dd') === format(currentDate, 'yyyy-MM-dd')
    );

    if (existingReservation) {
      // Edit existing reservation
      setSelectedTimeSlot({ ...timeSlot, reservation: existingReservation });
    } else {
      // Create new reservation
      setSelectedTimeSlot({ ...timeSlot, reservation: null });
    }
    
    setShowReservationModal(true);
  };

  const handleReservationSaved = () => {
    setShowReservationModal(false);
    setSelectedTimeSlot(null);
    loadReservations(); // Refresh reservations
  };

  const getReservationForTimeSlot = (timeSlotId, date) => {
    return reservations.find(r => 
      r.timeSlotId === timeSlotId && 
      format(new Date(r.date), 'yyyy-MM-dd') === format(date, 'yyyy-MM-dd')
    );
  };

  const getDashboardStats = () => {
    const today = format(new Date(), 'yyyy-MM-dd');
    const todayReservations = reservations.filter(r => 
      format(new Date(r.date), 'yyyy-MM-dd') === today
    );
    
    const paidReservations = todayReservations.filter(r => r.isPaid);
    const unpaidReservations = todayReservations.filter(r => !r.isPaid);
    const totalRevenue = paidReservations.reduce((sum, r) => sum + (r.totalPrice || 0), 0);

    return {
      totalReservations: todayReservations.length,
      paidReservations: paidReservations.length,
      unpaidReservations: unpaidReservations.length,
      totalRevenue
    };
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  if (businessProfiles.length === 0) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="text-center">
          <Building2 className="mx-auto h-12 w-12 text-gray-400" />
          <h3 className="mt-2 text-sm font-medium text-gray-900">Brak przypisanych obiektów</h3>
          <p className="mt-1 text-sm text-gray-500">
            Nie masz uprawnień do zarządzania żadnymi obiektami sportowymi.
          </p>
          <p className="mt-2 text-sm text-gray-500">
            Skontaktuj się z właścicielem obiektu, aby otrzymać zaproszenie jako agent.
          </p>
        </div>
      </div>
    );
  }

  const stats = getDashboardStats();

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="md:flex md:items-center md:justify-between mb-8">
        <div className="flex-1 min-w-0">
          <h2 className="text-2xl font-bold leading-7 text-gray-900 sm:text-3xl sm:truncate">
            Panel Agenta
          </h2>
          <p className="mt-1 text-sm text-gray-500">
            Zarządzaj harmonogramami i rezerwacjami obiektów sportowych
          </p>
        </div>
      </div>

      {error && (
        <div className="mb-6 bg-red-50 border border-red-200 rounded-md p-4 flex items-center">
          <AlertCircle className="h-5 w-5 text-red-400 mr-2" />
          <p className="text-sm text-red-600">{error}</p>
        </div>
      )}

      {/* Stats */}
      {selectedFacility && (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
          <div className="card">
            <div className="flex items-center">
              <div className="p-2 bg-blue-100 rounded-lg">
                <Calendar className="h-6 w-6 text-blue-600" />
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-500">Rezerwacje dziś</p>
                <p className="text-2xl font-semibold text-gray-900">{stats.totalReservations}</p>
              </div>
            </div>
          </div>

          <div className="card">
            <div className="flex items-center">
              <div className="p-2 bg-green-100 rounded-lg">
                <DollarSign className="h-6 w-6 text-green-600" />
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-500">Opłacone</p>
                <p className="text-2xl font-semibold text-gray-900">{stats.paidReservations}</p>
              </div>
            </div>
          </div>

          <div className="card">
            <div className="flex items-center">
              <div className="p-2 bg-yellow-100 rounded-lg">
                <Clock className="h-6 w-6 text-yellow-600" />
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-500">Nieopłacone</p>
                <p className="text-2xl font-semibold text-gray-900">{stats.unpaidReservations}</p>
              </div>
            </div>
          </div>

          <div className="card">
            <div className="flex items-center">
              <div className="p-2 bg-purple-100 rounded-lg">
                <DollarSign className="h-6 w-6 text-purple-600" />
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-500">Przychód dziś</p>
                <p className="text-2xl font-semibold text-gray-900">
                  {stats.totalRevenue.toFixed(2)} zł
                </p>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Main content */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Sidebar */}
        <div className="lg:col-span-1">
          <FacilitySelector
            businessProfiles={businessProfiles}
            selectedBusiness={selectedBusiness}
            onBusinessChange={setSelectedBusiness}
            facilities={facilities}
            selectedFacility={selectedFacility}
            onFacilityChange={setSelectedFacility}
            currentDate={currentDate}
            onDateChange={setCurrentDate}
          />
        </div>

        {/* Schedule view */}
        <div className="lg:col-span-3">
          {selectedFacility ? (
            <ScheduleView
              facility={selectedFacility}
              timeSlots={timeSlots}
              reservations={reservations}
              currentDate={currentDate}
              onTimeSlotClick={handleTimeSlotClick}
              getReservationForTimeSlot={getReservationForTimeSlot}
            />
          ) : (
            <div className="card text-center">
              <Building2 className="mx-auto h-12 w-12 text-gray-400" />
              <h3 className="mt-2 text-sm font-medium text-gray-900">Wybierz obiekt</h3>
              <p className="mt-1 text-sm text-gray-500">
                Wybierz obiekt sportowy z listy, aby zarządzać harmonogramem
              </p>
            </div>
          )}
        </div>
      </div>

      {/* Reservation Modal */}
      {showReservationModal && selectedTimeSlot && (
        <ReservationModal
          isOpen={showReservationModal}
          onClose={() => setShowReservationModal(false)}
          timeSlot={selectedTimeSlot}
          facility={selectedFacility}
          date={currentDate}
          onSaved={handleReservationSaved}
        />
      )}
    </div>
  );
}

export default AgentDashboard;