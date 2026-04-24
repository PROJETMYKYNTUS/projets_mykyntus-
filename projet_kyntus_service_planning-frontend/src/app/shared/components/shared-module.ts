import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HasPermissionDirective } from './directives/has-permission.directive';


@NgModule({
  declarations: [],
  imports: [
    CommonModule,
     HasPermissionDirective  
  ],
    exports: [
    CommonModule,
    HasPermissionDirective  // ← export correct
  ]
})
export class SharedModule { }
