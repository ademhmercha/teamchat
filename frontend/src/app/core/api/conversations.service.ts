import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConversationDetailDto,
  ConversationDto,
  ConversationListItemDto,
  ConversationParticipantDto,
  MessageHistoryResult,
  SharedAttachmentDto,
} from '../models/conversation.models';

@Injectable({ providedIn: 'root' })
export class ConversationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/conversations`;

  getMyConversations(): Observable<ConversationListItemDto[]> {
    return this.http.get<ConversationListItemDto[]>(this.baseUrl);
  }

  createDirect(userId: string): Observable<ConversationDto> {
    return this.http.post<ConversationDto>(`${this.baseUrl}/direct`, { userId });
  }

  getMessages(conversationId: string, before?: number, limit = 50): Observable<MessageHistoryResult> {
    let params = new HttpParams().set('limit', limit);
    if (before !== undefined && before !== null) {
      params = params.set('before', before);
    }

    return this.http.get<MessageHistoryResult>(`${this.baseUrl}/${conversationId}/messages`, { params });
  }

  markAsRead(conversationId: string, messageId?: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${conversationId}/read`, { messageId: messageId ?? null });
  }

  getDetail(conversationId: string): Observable<ConversationDetailDto> {
    return this.http.get<ConversationDetailDto>(`${this.baseUrl}/${conversationId}`);
  }

  getMembers(conversationId: string): Observable<ConversationParticipantDto[]> {
    return this.http.get<ConversationParticipantDto[]>(`${this.baseUrl}/${conversationId}/members`);
  }

  setArchived(conversationId: string, archived: boolean): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${conversationId}`, { archived });
  }

  leave(conversationId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${conversationId}/leave`, {});
  }

  getSharedAttachments(conversationId: string): Observable<SharedAttachmentDto[]> {
    return this.http.get<SharedAttachmentDto[]>(`${this.baseUrl}/${conversationId}/attachments`);
  }

  searchMessages(conversationId: string, query: string, limit = 30): Observable<MessageHistoryResult> {
    const params = new HttpParams().set('q', query).set('limit', limit);
    return this.http.get<MessageHistoryResult>(`${this.baseUrl}/${conversationId}/messages/search`, { params });
  }
}