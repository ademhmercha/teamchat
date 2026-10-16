import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PlatformTenant {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  createdAt: string;
  userCount: number;
  activeUserCount: number;
  groupCount: number;
  messageCount: number;
}

export interface UpdateTenantPayload {
  name?: string;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class PlatformService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/platform/tenants`;

  list(): Observable<PlatformTenant[]> {
    return this.http.get<PlatformTenant[]>(this.baseUrl);
  }

  get(id: string): Observable<PlatformTenant> {
    return this.http.get<PlatformTenant>(`${this.baseUrl}/${id}`);
  }

  update(id: string, payload: UpdateTenantPayload): Observable<PlatformTenant> {
    return this.http.patch<PlatformTenant>(`${this.baseUrl}/${id}`, payload);
  }
}
