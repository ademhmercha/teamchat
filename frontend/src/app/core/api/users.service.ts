import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult, UserListItem, UserProfile } from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api`;

  search(search: string | null, page: number, pageSize: number): Observable<PagedResult<UserListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) {
      params = params.set('search', search);
    }

    return this.http.get<PagedResult<UserListItem>>(`${this.baseUrl}/users`, { params });
  }

  getById(id: string): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.baseUrl}/users/${id}`);
  }
}
