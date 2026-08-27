const fs = require("fs");
const path = require("path");
const assert = require("assert");

const controllersDir = path.join("backend", "StackMeet.Api", "Controllers");
const controllerFiles = fs.readdirSync(controllersDir)
  .filter(name => name.endsWith("Controller.cs"))
  .sort();

const offenders = [];
for (const name of controllerFiles) {
  const source = fs.readFileSync(path.join(controllersDir, name), "utf8");
  if (/\bForbid\s*\(/.test(source)) offenders.push(name);
}

assert.deepStrictEqual(
  offenders,
  [],
  `Controllers using the custom authentication middleware must return an explicit HTTP 403 instead of ControllerBase.Forbid(), which requires an ASP.NET authentication scheme. Offenders: ${offenders.join(", ")}`
);

for (const required of [
  "CompetitionStateController.cs",
  "StackersController.cs",
  "CompetitionResultsController.cs",
  "CompetitionAssetsController.cs",
  "CompetitionsController.cs"
]) {
  const source = fs.readFileSync(path.join(controllersDir, required), "utf8");
  assert.match(source, /StatusCodes\.Status403Forbidden/);
}

console.log("Controller explicit-403 safety tests passed.");
