import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ConversationDto, ConversationListItemDto, MessageHistoryResult } from '../models/conversation.models';

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
}