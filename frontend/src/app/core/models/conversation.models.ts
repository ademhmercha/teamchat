export const ConversationType = {
  Direct: 0,
  Group: 1,
} as const;

export const MessageType = {
  Text: 0,
  Voice: 1,
  File: 2,
  Image: 3,
  System: 4,
} as const;

export interface ConversationParticipantDto {
  userId: string;
  displayName: string;
  avatarUrl: string | null;
}

export interface ConversationDto {
  id: string;
  type: number;
  name: string | null;
  createdAt: string;
  participants: ConversationParticipantDto[];
}

export interface ConversationListItemDto {
  id: string;
  type: number;
  name: string | null;
  otherUserId: string | null;
  otherDisplayName: string | null;
  otherAvatarUrl: string | null;
  lastMessageAt: string | null;
  lastMessagePreview: string | null;
  participantCount: number;
  unreadCount: number;
}

export interface GroupMemberDto {
  userId: string;
  displayName: string;
  avatarUrl: string | null;
}

export interface GroupDto {
  id: string;
  name: string;
  createdAt: string;
  isArchived: boolean;
  members: GroupMemberDto[];
}

export interface MessageAttachmentDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  durationSec: number | null;
}

export interface MessageDto {
  sequence: number;
  id: string;
  conversationId: string;
  senderId: string;
  senderDisplayName: string;
  senderAvatarUrl: string | null;
  type: number;
  content: string | null;
  attachmentId: string | null;
  createdAt: string;
  edited: boolean;
  deleted: boolean;
  attachment: MessageAttachmentDto | null;
}

export interface MessageCursor {
  sequence: number;
}

export interface MessageHistoryResult {
  items: MessageDto[];
  nextCursor: MessageCursor | null;
  hasMore: boolean;
}