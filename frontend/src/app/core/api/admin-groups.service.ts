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

  getGroup(groupId: string): Observable<GroupDto> {
    return this.http.get<GroupDto>(`${this.baseUrl}/${groupId}`);
  }

  createGroup(name: string, memberIds: string[]): Observable<GroupDto> {
    return this.http.post<GroupDto>(this.baseUrl, { name, memberIds });
  }

  renameGroup(groupId: string, name: string): Observable<GroupDto> {
    return this.http.put<GroupDto>(`${this.baseUrl}/${groupId}`, { name });
  }

  updateMembers(groupId: string, memberIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${groupId}/members`, { memberIds });
  }

  archiveGroup(groupId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${groupId}`);
  }

  unarchiveGroup(groupId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${groupId}/unarchive`, {});
  }

  deleteGroup(groupId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${groupId}/permanent`);
  }
}
