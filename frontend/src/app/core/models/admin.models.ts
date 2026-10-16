export interface DailyMessageCount {
  date: string;
  count: number;
}

export interface ActiveGroup {
  conversationId: string;
  name: string;
  messageCount: number;
  memberCount: number;
}

export interface AdminStats {
  activeUsers: number;
  totalUsers: number;
  activeGroups: number;
  messagesLast7Days: number;
  messagesPerDay: DailyMessageCount[];
  topGroups: ActiveGroup[];
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  role: string;
  password: string;
}

export interface ResetUserPasswordRequest {
  password: string;
}

export interface UpdateUserRequest {
  displayName?: string;
  role?: string;
  isActive?: boolean;
}
