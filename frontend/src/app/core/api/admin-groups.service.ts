import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GroupDto } from '../models/conversation.models';

@Injectable({ providedIn: 'root' })
export class AdminGroupsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/groups`;

  getGroups(): Observable<GroupDto[]> {
    return this.http.get<GroupDto[]>(this.baseUrl);
  }

  createGroup(name: string, memberIds: string[]): Observable<GroupDto> {
    return this.http.post<GroupDto>(this.baseUrl, { name, memberIds });
  }

  updateMembers(groupId: string, memberIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${groupId}/members`, { memberIds });
  }

  archiveGroup(groupId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${groupId}`);
  }
}
