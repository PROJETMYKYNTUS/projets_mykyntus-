import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FloorDetailComponent } from './floor-detail.component';

describe('FloorDetail', () => {
  let component: FloorDetailComponent;
  let fixture: ComponentFixture<FloorDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FloorDetailComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FloorDetailComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
