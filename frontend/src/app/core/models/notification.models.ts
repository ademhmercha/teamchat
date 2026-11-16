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
