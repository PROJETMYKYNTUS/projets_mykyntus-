import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MediaAsset } from './media.service';

export type AudienceTarget =
  'All' | 'Employees' | 'Managers' | 'Admins' |
  'Pilotes' | 'Coaches' | 'RPs' | 'Audits' |
  'EquipeFormation' | 'Custom';
export type CampaignStatus = 'Draft' | 'Scheduled' | 'Sending' | 'Sent' | 'Cancelled' | 'Failed';

export interface CreateNewsletterDto {
  title: string;
  subject: string;
  textContent: string;
  coverImageUrl?: string;
  mediaIds?: number[];
}

export interface NewsletterResponse {
  id: number;
  title: string;
  subject: string;
  htmlContent: string;
  textContent?: string;
  coverImageUrl?: string;
  createdAt: string;
  updatedAt?: string;
  createdByUserId: string;
  campaignsCount: number;
  media?: MediaAsset[];
}

export interface CreatePublicationDto {
  title: string;
  subject: string;
  textContent: string;
  mediaIds?: number[];
  audienceTarget?: AudienceTarget;
  /** GUIDs employés — même logique que formation continue. */
  beneficiaryUserIds?: string[];
  mode: 'draft' | 'publish' | 'schedule';
  scheduledAt?: string | null;
  campaignName?: string;
}

export interface PublicationResponse {
  newsletter: NewsletterResponse;
  campaign?: CampaignResponse | null;
}

export interface CreateCampaignDto {
  name: string;
  newsletterId: number;
  audienceTarget: AudienceTarget;
  scheduledAt?: string | null;
}

export interface CampaignResponse {
  id: number;
  name: string;
  newsletterId: number;
  newsletterTitle: string;
  newsletterSubject: string;
  audienceTarget: AudienceTarget;
  status: CampaignStatus;
  scheduledAt?: string | null;
  publishedAt?: string | null;
  totalRecipients: number;
  createdAt: string;
}

export interface EmployeeNewsletter {
  analyticsId: number;
  campaignId: number;
  campaignName: string;
  newsletterTitle: string;
  newsletterSubject: string;
  htmlContent: string;
  textContent?: string;
  coverImageUrl?: string;
  isRead: boolean;
  readAt?: string | null;
  receivedAt: string;
  media?: MediaAsset[];
}

export interface CampaignAnalytics {
  campaignId: number;
  campaignName: string;
  totalRecipients: number;
  totalRead: number;
  totalUnread: number;
  readRate: number;
}

@Injectable({ providedIn: 'root' })
export class NewsletterService {
  private base = `${environment.apiUrl}/newsletter`;
  private employeeBase = `${environment.apiUrl}/my-newsletters`;

  constructor(private http: HttpClient) {}

  getNewsletters(): Observable<NewsletterResponse[]> {
    return this.http.get<NewsletterResponse[]>(this.base);
  }

  getNewsletterById(id: number): Observable<NewsletterResponse> {
    return this.http.get<NewsletterResponse>(`${this.base}/${id}`);
  }

  createNewsletter(dto: CreateNewsletterDto): Observable<NewsletterResponse> {
    return this.http.post<NewsletterResponse>(this.base, dto);
  }

  createPublication(dto: CreatePublicationDto): Observable<PublicationResponse> {
    return this.http.post<PublicationResponse>(`${this.base}/publications`, dto);
  }

  getCampaigns(): Observable<CampaignResponse[]> {
    return this.http.get<CampaignResponse[]>(`${this.base}/campaigns`);
  }

  publishCampaign(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/campaigns/${id}/publish`, {});
  }

  scheduleCampaign(id: number, scheduledAt: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/campaigns/${id}/schedule`, scheduledAt);
  }

  cancelCampaign(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/campaigns/${id}/cancel`, {});
  }

  getAnalytics(id: number): Observable<CampaignAnalytics> {
    return this.http.get<CampaignAnalytics>(`${this.base}/campaigns/${id}/analytics`);
  }

  getMyNewsletters(_group?: string): Observable<EmployeeNewsletter[]> {
    return this.http.get<EmployeeNewsletter[]>(this.employeeBase);
  }

  markAsRead(analyticsId: number): Observable<{ message: string }> {
    return this.http.patch<{ message: string }>(`${this.employeeBase}/${analyticsId}/read`, {});
  }
}
