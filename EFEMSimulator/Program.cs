using EFEMSimulator;
using EFEMSimulator.Models;

var lp1      = new LoadPort(1, waferCount: 25);
var lp2      = new LoadPort(2, waferCount: 0);
var buffer   = new PreHeatBuffer();
var chambers = new Chamber[] { new Chamber(1), new Chamber(2) };
var robot    = new Robot();

var scheduler = new Scheduler(lp1, lp2, robot, buffer, chambers);
scheduler.RunSimulation();
