import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PlanningService } from '../../../../core/services/planning.service';
import { JwtRoleService } from '../../../../core/auth/jwt-role.service';
import { NotificationService, PlanningNotification } from '../../../../core/services/notification.service';
import { NewsletterService, EmployeeNewsletter } from '../../../../core/services/newsletter.service';
import { ReclamationEmployeeComponent } from '../../../reclamation/employee/reclamation-employee.component';

interface DayAssignment {
  assignmentId: number;
  day: string;
  assignedDate: string;
  shiftLabel: string;
  startTime: string;
  endTime: string;
  breakTime: string | null;
  isOnLeave: boolean;
  isHoliday: boolean;
  holidayName: string;
  isSaturday: boolean;
  absenceType: string | null;
}

interface MyPlanning {
  weekCode: string;
  weekStartDate: string;
  subServiceName: string;
  days: DayAssignment[];
}

@Component({
  selector: 'app-dashboard-employee',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReclamationEmployeeComponent],
  templateUrl: './dashboard-employee.component.html',
  styleUrls: ['./dashboard-employee.component.css']
})
export class DashboardEmployeeComponent implements OnInit, OnDestroy {

  // ── Vues ──
  currentView: 'home' | 'planning' | 'conges' | 'settings' | 'newsletters' | 'reclamations' = 'home';

  // ── User ──
  planning: MyPlanning | null = null;
  history: MyPlanning[] = [];
  loading = false;
  currentWeek = '';
  userId = 0;
  userName = 'Employé';
  userInitials = 'EK';
  userRole = 'Employé';

  // ── Notifications ──
  notifications: PlanningNotification[] = [];
  unreadCount = 0;
  showNotifications = false;

  // ── Toast ──
  showToast = false;
  toastMessage = '';

  myNewsletters: EmployeeNewsletter[] = [];
  selectedNewsletter: EmployeeNewsletter | null = null;
  nlLoading = false;
  unreadNewsletters = 0;

  readonly dayLabels: Record<string, string> = {
    Monday: 'Lundi', Tuesday: 'Mardi', Wednesday: 'Mercredi',
    Thursday: 'Jeudi', Friday: 'Vendredi', Saturday: 'Samedi'
  };

  readonly menuItems = [
    {
      id: 'planning',
      label: 'Mon Planning',
      desc: 'Consultez vos shifts et horaires de la semaine',
      icon: 'calendar',
      color: '#3b82f6'
    },
    {
      id: 'conges',
      label: 'Mes Congés',
      desc: 'Gérez vos demandes de congés et absences',
      icon: 'beach',
      color: '#10b981'
    },
    {
      id: 'settings',
      label: 'Paramètres',
      desc: 'Personnalisez votre compte et préférences',
      icon: 'settings',
      color: '#8b5cf6'
    }
  ];

  constructor(
    private planningService: PlanningService,
    private notificationService: NotificationService,
    private newsletterSvc: NewsletterService,
    private readonly jwtRole: JwtRoleService,
  ) {}

  /** Employee = Pilote : deux labels de rôle donnent le même accès Documentation employé. */
  get showDocumentationPilotButton(): boolean {
    return this.jwtRole.hasDocumentationPilotAccess();
  }

  ngOnInit(): void {
  const user = JSON.parse(localStorage.getItem('user') || '{}');
  this.userId   = user?.id;
  this.userName = user?.username || 'Employé';
  console.log('👤 user object complet:', user);        // ← AJOUTEZ
  console.log('👤 userId pour SignalR:', this.userId);
  this.loadMyNewsletters();  // ← appelé ici, nlLoading = false après
  this.userInitials = this.userName.substring(0, 2).toUpperCase();

  if (!this.userId) return;

    // ── SignalR ──
    this.notificationService.connect(this.userId);

this.notificationService.notifications$.subscribe(notifs => {
  console.log('📋 notifications$ émis:', notifs.length, notifs); // ← AJOUTEZ
  this.notifications = notifs;

  const latest = notifs[0];
  if (latest && !latest.read) {
    this.showToastMessage(latest.message);
    // ✅ Recharger TOUJOURS, sans condition sur la vue
    this.loadCurrentPlanning();
    this.loadHistory();
  }
});

    this.notificationService.unreadCount$.subscribe(count => {
      this.unreadCount = count;
    });

    this.loadHistory();
  }

  ngOnDestroy(): void {
    this.notificationService.disconnect();
  }

  // ── Navigation ──
navigateTo(view: 'home' | 'planning' | 'conges' | 'settings' | 'newsletters' | 'reclamations'):void {
  this.currentView = view;
  if (view === 'planning' && !this.planning) {
    this.loadCurrentPlanning();
  }
  if (view === 'newsletters') {
    this.loadMyNewsletters();
  }
}
  // ── Planning ──
  loadCurrentPlanning(): void {
    this.loading = true;
    this.planningService.getMyCurrentPlanning(this.userId).subscribe({
      next: (data) => {
        this.planning    = data;
        this.currentWeek = data.weekCode;
        this.loading     = false;
      },
      error: () => { this.planning = null; this.loading = false; }
    });
  }

  loadWeek(weekCode: string): void {
    this.loading     = true;
    this.currentWeek = weekCode;
    this.planningService.getMyPlanning(weekCode, this.userId).subscribe({
      next: (data) => { this.planning = data; this.loading = false; },
      error: () => { this.planning = null; this.loading = false; }
    });
  }

  onWeekChange(event: any): void {
    const weekCode = event.target.value;
    if (weekCode) this.loadWeek(weekCode);
  }

  loadHistory(): void {
    this.planningService.getMyHistory(this.userId).subscribe({
      next: (data: MyPlanning[]) => this.history = data,
      error: () => this.history = []
    });
  }

  // ── Notifications ──
  toggleNotifications(): void {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) this.notificationService.markAllRead();
  }
   
  onNotifClick(n: PlanningNotification): void {
  this.showNotifications = false;
  // Si c'est une notification réclamation → naviguer vers la vue réclamations
  if (n.type === 'reclamation') {
    this.navigateTo('reclamations');
  } else {
    // Comportement existant pour planning
    this.navigateTo('planning');
  }
}
  showToastMessage(msg: string): void {
    this.toastMessage = msg;
    this.showToast    = true;
    setTimeout(() => this.showToast = false, 5000);
  }

  // ── Helpers ──
  getDayLabel(day: string): string { return this.dayLabels[day] || day; }

  getDateForDay(assignedDate: string): string {
    const d = new Date(assignedDate);
    return `${d.getDate().toString().padStart(2,'0')}/${(d.getMonth()+1).toString().padStart(2,'0')}`;
  }

  getWorkDaysCount(): number {
    return this.planning?.days.filter(d => !d.isHoliday && !d.isOnLeave).length ?? 0;
  }

  getTotalHours(): number {
    if (!this.planning) return 0;
    return Math.round(this.planning.days
      .filter(d => !d.isHoliday && !d.isOnLeave && d.startTime && d.endTime)
      .reduce((total, d) => {
        const [sh, sm] = d.startTime.split(':').map(Number);
        const [eh, em] = d.endTime.split(':').map(Number);
        return total + ((eh * 60 + em) - (sh * 60 + sm)) / 60 - 1;
      }, 0));
  }

  getRowClass(day: DayAssignment): string {
    if (day.isHoliday) return 'emp-row-ferie';
    if (day.isOnLeave) return 'emp-row-conge';
    return '';
  }

  getShiftBg(label: string): string {
    const map: Record<string, string> = {
      'Shift 1': '#eff6ff', 'Shift 2': '#fffbeb',
      'Shift 3': '#f5f3ff', 'OFF': '#f8fafc'
    };
    return map[label] || '#f0fdf4';
  }

  getShiftColor(label: string): string {
    const map: Record<string, string> = {
      'Shift 1': '#1d4ed8', 'Shift 2': '#b45309',
      'Shift 3': '#7c3aed', 'OFF': '#64748b'
    };
    return map[label] || '#166534';
  }

  getCurrentDate(): string {
    return new Date().toLocaleDateString('fr-FR', {
      weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
    });
  }
loadMyNewsletters(): void {
  this.nlLoading = true;
  console.log('🔵 loadMyNewsletters START');
  this.newsletterSvc.getMyNewsletters('Employees').subscribe({
    next: (data) => {
      console.log('✅ DATA:', data);
      console.log('✅ IS ARRAY:', Array.isArray(data));
      this.myNewsletters = data ?? [];
      this.unreadNewsletters = this.myNewsletters.filter(n => !n.isRead).length;
      this.nlLoading = false;
    },
    error: (err) => {
      console.log('❌ ERREUR:', err);
      this.nlLoading = false;
    }
  });
}
 
openNewsletter(nl: EmployeeNewsletter): void {
  this.selectedNewsletter = nl;
  if (!nl.isRead) {
    this.newsletterSvc.markAsRead(nl.analyticsId).subscribe({
      next: () => {
        nl.isRead = true;
        nl.readAt = new Date().toISOString();
        this.unreadNewsletters = Math.max(0, this.unreadNewsletters - 1);
      }
    });
  }
}
 
closeNewsletter(): void {
  this.selectedNewsletter = null;
}
 
getReadCount(): number {
  return this.myNewsletters.filter(n => n.isRead).length;
}
 
getUnreadCount(): number {
  return this.myNewsletters.filter(n => !n.isRead).length;
}
}