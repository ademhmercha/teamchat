export const CallType = {
  Audio: 0,
  Video: 1,
} as const;

export const CallStatus = {
  Ringing: 0,
  Accepted: 1,
  Rejected: 2,
  Missed: 3,
  Ended: 4,
  Failed: 5,
} as const;

export interface CallSessionDto {
  callId: string;
  callerId: string;
  callerDisplayName: string;
  calleeId: string;
  calleeDisplayName: string;
  callerAvatarUrl?: string | null;
  calleeAvatarUrl?: string | null;
  type: number;
  status: number;
  conversationId: string;
  startedAt: string;
}

export interface IceServerDto {
  urls: string[];
  username: string | null;
  credential: string | null;
}

export interface CallHistoryItemDto {
  id: string;
  otherUserId: string;
  otherDisplayName: string;
  type: number;
  status: number;
  startedAt: string;
  durationSeconds: number | null;
  outgoing: boolean;
}

export interface IceCandidatePayload {
  callId: string;
  candidate: string;
  sdpMid: string | null;
  sdpMLineIndex: number | null;
}

export interface CallEndedPayload {
  callId: string;
  status: number;
}

export interface SdpPayload {
  callId: string;
  sdp: string;
}
