export interface UserProfile {
  id: string;
  displayName: string;
  email: string;
  avatarUrl: string | null;
  role: string;
  isActive: boolean;
}

export interface UserListItem {
  id: string;
  displayName: string;
  email: string;
  avatarUrl: string | null;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile | null;
}
