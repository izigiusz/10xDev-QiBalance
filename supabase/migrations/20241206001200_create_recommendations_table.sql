-- Migration: Create recommendations table
-- Purpose: Create the recommendations table to store AI-generated health recommendations for users
-- Affected tables: recommendations (create)
-- Dependencies: Supabase Auth (users table managed by auth system)
-- Row-Level Security: Enabled with user-specific access policies

-- Enable UUID extension if not already enabled
create extension if not exists "uuid-ossp";

-- Create recommendations table
-- This table stores AI-generated health recommendations for authenticated users
create table public.recommendations (
    -- Primary key using UUID for security and scalability
    recommendation_id uuid primary key default uuid_generate_v4(),
    
    -- Foreign key reference to auth.users.email
    -- Using varchar to match auth.users.email type
    user_id varchar not null,
    
    -- Timestamp when the recommendation was generated
    -- Using timestamptz for timezone awareness
    date_generated timestamptz not null default now(),
    
    -- The actual recommendation text content
    -- Using text for unlimited length recommendations
    recommendation_text text not null,
    
    -- Add foreign key constraint to auth.users
    constraint fk_recommendations_user_id 
        foreign key (user_id) references auth.users(email) 
        on delete cascade
);

-- Create index on user_id for performance optimization
-- This index will speed up queries filtering by user
create index idx_recommendations_user_id on public.recommendations(user_id);

-- Create index on date_generated for chronological queries
-- This index will help with ordering recommendations by date
create index idx_recommendations_date_generated on public.recommendations(date_generated desc);

-- Enable Row Level Security on recommendations table
-- This ensures users can only access their own recommendations
alter table public.recommendations enable row level security;

-- RLS Policy: Allow authenticated users to select their own recommendations
-- This policy ensures users can only view recommendations associated with their email
create policy "Users can view own recommendations" on public.recommendations
    for select
    to authenticated
    using (user_id = auth.email());

-- RLS Policy: Allow authenticated users to insert their own recommendations
-- This policy ensures users can only create recommendations for themselves
create policy "Users can insert own recommendations" on public.recommendations
    for insert
    to authenticated
    with check (user_id = auth.email());

-- RLS Policy: Allow authenticated users to update their own recommendations
-- This policy ensures users can only modify their own recommendations
create policy "Users can update own recommendations" on public.recommendations
    for update
    to authenticated
    using (user_id = auth.email())
    with check (user_id = auth.email());

-- RLS Policy: Allow authenticated users to delete their own recommendations
-- This policy ensures users can only delete their own recommendations
create policy "Users can delete own recommendations" on public.recommendations
    for delete
    to authenticated
    using (user_id = auth.email());

-- Add comments to the table and columns for documentation
comment on table public.recommendations is 'Stores AI-generated health recommendations for authenticated users';
comment on column public.recommendations.recommendation_id is 'Unique identifier for each recommendation';
comment on column public.recommendations.user_id is 'Email of the user who owns this recommendation (FK to auth.users.email)';
comment on column public.recommendations.date_generated is 'Timestamp when the recommendation was generated';
comment on column public.recommendations.recommendation_text is 'The actual recommendation content provided to the user'; 