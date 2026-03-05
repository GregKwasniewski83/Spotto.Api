-- User table
CREATE TABLE user (
    id UUID PRIMARY KEY,
    full_name TEXT,
    email TEXT,
    preferences JSONB,
    membership_type TEXT,
    created_at TIMESTAMP
);

-- SkillLevel table
CREATE TABLE skill_level (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES user(id),
    activity TEXT,
    level TEXT,
    certification TEXT
);

-- Facility table
CREATE TABLE facility (
    id UUID PRIMARY KEY,
    name TEXT,
    location TEXT,
    amenities JSONB,
    operating_hours TEXT,
    policies TEXT
);

-- Activity table
CREATE TABLE activity (
    id UUID PRIMARY KEY,
    name TEXT,
    pricing NUMERIC,
    requirements TEXT
);

-- Court table
CREATE TABLE court (
    id UUID PRIMARY KEY,
    facility_id UUID REFERENCES facility(id),
    type TEXT,
    specifications TEXT,
    equipment JSONB
);

-- Equipment table
CREATE TABLE equipment (
    id UUID PRIMARY KEY,
    name TEXT,
    rental_price NUMERIC,
    is_available BOOLEAN
);

-- Reservation table
CREATE TABLE reservation (
    id UUID PRIMARY KEY,
    facility_id UUID REFERENCES facility(id),
    activity_id UUID REFERENCES activity(id),
    participants JSONB,
    equipment JSONB,
    total_price NUMERIC,
    status TEXT,
    reserved_at TIMESTAMP,
    time_slot JSONB
);

-- ...other tables for AI, Review, Achievement, PartnerMatch, Tournament, Payment, Notification, SearchFilters...
