import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NotificationListDto } from '../models/notification.models';

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/notifications`;

  getMine(unreadOnly = false, limit = 50): Observable<NotificationListDto> {
    const params = new HttpParams().set('unreadOnly', unreadOnly).set('limit', limit);
    return this.http.get<NotificationListDto>(this.baseUrl, { params });
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/read-all`, {});
  }
}
