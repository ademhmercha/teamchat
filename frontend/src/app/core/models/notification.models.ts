export const NotificationType = {
  GroupAdded: 0,
  MissedCall: 1,
} as const;

export interface NotificationDto {
  id: string;
  type: number;
  title: string;
  body: string;
  conversationId: string | null;
  actorUserId: string | null;
  actorDisplayName: string | null;
  createdAt: string;
  read: boolean;
}

export interface NotificationListDto {
  items: NotificationDto[];
  unreadCount: number;
}
