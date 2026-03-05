import React, { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { LogOut, User, Calendar, Menu, X, Building } from 'lucide-react';

function Navbar() {
  const { user, logout } = useAuth();
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const handleLogout = () => {
    logout();
    setIsMenuOpen(false);
  };

  const getUserInitials = (firstName, lastName) => {
    return `${firstName?.[0] || ''}${lastName?.[0] || ''}`.toUpperCase();
  };

  const getUserRoleDisplay = (roles) => {
    if (!roles || roles.length === 0) return 'Użytkownik';
    
    const roleNames = {
      'Player': 'Gracz',
      'Business': 'Właściciel',
      'Agent': 'Agent',
      'Admin': 'Administrator'
    };
    
    return roles.map(role => roleNames[role] || role).join(', ');
  };

  return (
    <nav className="bg-white shadow-sm border-b border-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between h-16">
          {/* Logo and brand */}
          <div className="flex items-center">
            <div className="flex-shrink-0">
              <div className="flex items-center">
                <div className="h-8 w-8 bg-primary-600 rounded-lg flex items-center justify-center">
                  <Building className="h-5 w-5 text-white" />
                </div>
                <span className="ml-2 text-xl font-bold text-gray-900">PlaySpace</span>
              </div>
            </div>
            
            {/* Navigation links - desktop */}
            <div className="hidden md:flex md:ml-10 md:space-x-8">
              {user?.roles?.includes('Agent') && (
                <a
                  href="/agent"
                  className="text-gray-900 hover:text-primary-600 px-3 py-2 rounded-md text-sm font-medium flex items-center"
                >
                  <Calendar className="h-4 w-4 mr-2" />
                  Panel Agenta
                </a>
              )}
            </div>
          </div>

          {/* User menu - desktop */}
          <div className="hidden md:flex md:items-center md:space-x-4">
            {user && (
              <div className="flex items-center space-x-3">
                <div className="text-right">
                  <div className="text-sm font-medium text-gray-900">
                    {user.isAnonymous ? 'Gość' : `${user.firstName} ${user.lastName}`}
                  </div>
                  <div className="text-xs text-gray-500">
                    {user.isAnonymous ? 'Tryb Gościa' : getUserRoleDisplay(user.roles)}
                  </div>
                </div>
                
                <div className="h-8 w-8 bg-primary-100 rounded-full flex items-center justify-center">
                  <span className="text-sm font-medium text-primary-700">
                    {user.isAnonymous ? 'G' : getUserInitials(user.firstName, user.lastName)}
                  </span>
                </div>
                
                {!user.isAnonymous ? (
                  <button
                    onClick={handleLogout}
                    className="text-gray-500 hover:text-gray-700 p-2 rounded-md"
                    title="Wyloguj się"
                  >
                    <LogOut className="h-4 w-4" />
                  </button>
                ) : (
                  <a
                    href="/login"
                    className="text-primary-600 hover:text-primary-700 px-3 py-2 rounded-md text-sm font-medium"
                  >
                    Zaloguj się
                  </a>
                )}
              </div>
            )}
          </div>

          {/* Mobile menu button */}
          <div className="md:hidden flex items-center">
            <button
              onClick={() => setIsMenuOpen(!isMenuOpen)}
              className="text-gray-500 hover:text-gray-700 p-2 rounded-md"
            >
              {isMenuOpen ? (
                <X className="h-6 w-6" />
              ) : (
                <Menu className="h-6 w-6" />
              )}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile menu */}
      {isMenuOpen && (
        <div className="md:hidden">
          <div className="px-2 pt-2 pb-3 space-y-1 sm:px-3 bg-white border-t border-gray-200">
            {user?.roles?.includes('Agent') && (
              <a
                href="/agent"
                className="text-gray-900 hover:text-primary-600 block px-3 py-2 rounded-md text-base font-medium flex items-center"
                onClick={() => setIsMenuOpen(false)}
              >
                <Calendar className="h-4 w-4 mr-2" />
                Panel Agenta
              </a>
            )}
            
            {user && (
              <div className="px-3 py-2 space-y-2">
                <div className="flex items-center space-x-3">
                  <div className="h-10 w-10 bg-primary-100 rounded-full flex items-center justify-center">
                    <span className="text-sm font-medium text-primary-700">
                      {user.isAnonymous ? 'G' : getUserInitials(user.firstName, user.lastName)}
                    </span>
                  </div>
                  <div>
                    <div className="text-base font-medium text-gray-900">
                      {user.isAnonymous ? 'Gość' : `${user.firstName} ${user.lastName}`}
                    </div>
                    <div className="text-sm text-gray-500">
                      {user.isAnonymous ? 'Tryb Gościa' : getUserRoleDisplay(user.roles)}
                    </div>
                  </div>
                </div>
                
                {!user.isAnonymous ? (
                  <button
                    onClick={handleLogout}
                    className="w-full text-left text-gray-900 hover:text-red-600 px-3 py-2 rounded-md text-base font-medium flex items-center"
                  >
                    <LogOut className="h-4 w-4 mr-2" />
                    Wyloguj się
                  </button>
                ) : (
                  <a
                    href="/login"
                    className="w-full text-left text-primary-600 hover:text-primary-700 px-3 py-2 rounded-md text-base font-medium flex items-center"
                    onClick={() => setIsMenuOpen(false)}
                  >
                    <User className="h-4 w-4 mr-2" />
                    Zaloguj się
                  </a>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </nav>
  );
}

export default Navbar;