import React from "react";
import {
  BrowserRouter as Router,
  Routes,
  Route,
  Navigate,
} from "react-router-dom";
import { AuthProvider, useAuth } from "./context/AuthContext";
import LoginPage from "./pages/LoginPage";
import RegisterPage from "./pages/RegisterPage";
import AgentDashboard from "./pages/AgentDashboard";
import Navbar from "./components/Navbar";
import "./App.css";

function ProtectedRoute({ children, allowedRoles = [] }) {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" />;
  }

  // Skip role check for anonymous users temporarily
  if (
    !user.isAnonymous &&
    allowedRoles.length > 0 &&
    !allowedRoles.some((role) => user.roles?.includes(role))
  ) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-800 mb-4">
            Brak uprawnień
          </h2>
          <p className="text-gray-600">Nie masz uprawnień do tego obszaru.</p>
        </div>
      </div>
    );
  }

  return children;
}

function AppRoutes() {
  const { user } = useAuth();

  return (
    <Router>
      <div className="min-h-screen bg-gray-50">
        {user && <Navbar />}

        <Routes>
          <Route
            path="/login"
            element={
              user && !user.isAnonymous ? <Navigate to="/" /> : <LoginPage />
            }
          />

          <Route
            path="/register"
            element={
              user && !user.isAnonymous ? <Navigate to="/" /> : <RegisterPage />
            }
          />

          <Route
            path="/"
            element={
              <ProtectedRoute>
                <Navigate to="/agent" />
              </ProtectedRoute>
            }
          />

          <Route
            path="/agent"
            element={
              <ProtectedRoute allowedRoles={["Agent"]}>
                <AgentDashboard />
              </ProtectedRoute>
            }
          />

          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </div>
    </Router>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  );
}

export default App;
