import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateUserRequest, UpdateUserRequest } from '../models/admin.models';
import { PagedResult, UserProfile } from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/users`;

  list(search: string | null, page: number, pageSize: number): Observable<PagedResult<UserProfile>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) {
      params = params.set('search', search);
    }

    return this.http.get<PagedResult<UserProfile>>(this.baseUrl, { params });
  }

  create(request: CreateUserRequest): Observable<UserProfile> {
    return this.http.post<UserProfile>(this.baseUrl, request);
  }

  update(id: string, request: UpdateUserRequest): Observable<UserProfile> {
    return this.http.patch<UserProfile>(`${this.baseUrl}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  resetPassword(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/reset-password`, {});
  }
}
