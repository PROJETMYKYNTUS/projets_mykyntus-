// new-employee-manager.component.ts — VERSION COMPLÈTE
import { Component, OnInit, ChangeDetectorRef } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import { CongeService, EmployeeSimple } from "../../services/conge.service";
import { PlanningService, SubServiceSimple } from "../../services/planning.service";

@Component({
  selector: "app-new-employee-manager",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./new-employee-manager.component.html",
  styleUrls: ["./new-employee-manager.component.css"]
})
export class NewEmployeeManagerComponent implements OnInit {

  subServices:        SubServiceSimple[] = [];
  newEmployees:       EmployeeSimple[]   = [];
  subServiceId        = 0;
  loading             = false;
  error               = "";
  successMsg          = "";
  hasUnconfiguredSlot = false;

  constructor(
    private congeService:    CongeService,
    private planningService: PlanningService,
    private router:          Router,
    private cdr:             ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.planningService.getSubServices().subscribe({
      next: (data: SubServiceSimple[]) => {
        this.subServices = data;
        if (data.length > 0) {
          this.subServiceId = data[0].id;
          this.loadNewEmployees();
        }
        this.cdr.detectChanges();
      }
    });
  }

  onSubServiceChange(): void { this.loadNewEmployees(); }

  loadNewEmployees(): void {
    if (!this.subServiceId) return;
    this.loading = true;
    this.congeService.getNewEmployees(this.subServiceId).subscribe({
      next: (data: EmployeeSimple[]) => {
        this.newEmployees        = data;
        this.hasUnconfiguredSlot = data.some(e => e.saturdaySlot === 0);
        this.loading             = false;
        this.cdr.detectChanges();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  setSaturdaySlot(userId: number, slot: number): void {
    this.congeService.setSaturdaySlot({ userId, slot }).subscribe({
      next: () => {
        this.successMsg = "Slot samedi configuré !";
        this.loadNewEmployees();
        setTimeout(() => { this.successMsg = ""; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message ?? "Erreur.";
        this.cdr.detectChanges();
      }
    });
  }

  // ✅ Retirer le statut nouveau manuellement
  removeNewStatus(userId: number, fullName: string): void {
    if (!confirm("Retirer le statut Nouveau de " + fullName + " ?\nCe salarié ne travaillera plus tous les samedis."))
      return;

    this.congeService.setNewEmployeeStatus(userId, false).subscribe({
      next: () => {
        this.successMsg = fullName + " n'est plus marqué comme nouveau.";
        this.loadNewEmployees();
        setTimeout(() => { this.successMsg = ""; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = err.error?.message ?? "Erreur.";
        this.cdr.detectChanges();
      }
    });
  }

  goBack(): void { this.router.navigate(["/dashboard"]); }
}