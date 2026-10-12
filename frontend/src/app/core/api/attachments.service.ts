import { HttpClient, HttpEventType, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MessageDto } from '../models/conversation.models';

export interface UploadEvent {
  progress: number;
  message?: MessageDto;
}

/**
 * Televersement et lecture des pieces jointes.
 * Les fichiers sont prives : ils sont recuperes via HttpClient (le token est
 * ajoute par l'intercepteur), jamais via une URL publique, puis exposes sous
 * forme d'URL d'objet (blob:) mise en cache par piece jointe.
 */
@Injectable({ providedIn: 'root' })
export class AttachmentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api`;
  private readonly blobUrls = new Map<string, string>();

  upload(
    conversationId: string,
    file: File | Blob,
    options: { fileName: string; isVoice?: boolean; durationSec?: number },
  ): Observable<UploadEvent> {
    const form = new FormData();
    form.append('file', file, options.fileName);
    form.append('isVoice', options.isVoice ? 'true' : 'false');
    if (options.durationSec !== undefined) {
      form.append('durationSec', String(options.durationSec));
    }

    const request = new HttpRequest('POST', `${this.baseUrl}/conversations/${conversationId}/attachments`, form, {
      reportProgress: true,
    });

    return this.http.request<MessageDto>(request).pipe(
      map((event): UploadEvent | null => {
        if (event.type === HttpEventType.UploadProgress) {
          const total = event.total ?? 0;
          return { progress: total > 0 ? Math.round((event.loaded / total) * 100) : 0 };
        }
        if (event.type === HttpEventType.Response && event.body) {
          return { progress: 100, message: event.body };
        }
        return null;
      }),
      filter((event): event is UploadEvent => event !== null),
    );
  }

  getBlobUrl(attachmentId: string): Observable<string> {
    const cached = this.blobUrls.get(attachmentId);
    if (cached) {
      return new Observable<string>((subscriber) => {
        subscriber.next(cached);
        subscriber.complete();
      });
    }

    return this.http
      .get(`${this.baseUrl}/attachments/${attachmentId}`, { responseType: 'blob' })
      .pipe(
        map((blob) => {
          const url = URL.createObjectURL(blob);
          this.blobUrls.set(attachmentId, url);
          return url;
        }),
      );
  }

  download(attachmentId: string, fileName: string): void {
    this.getBlobUrl(attachmentId).subscribe({
      next: (url) => {
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
      },
      error: () => undefined,
    });
  }

  releaseAll(): void {
    this.blobUrls.forEach((url) => URL.revokeObjectURL(url));
    this.blobUrls.clear();
  }
}
