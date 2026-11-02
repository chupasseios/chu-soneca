import ts from 'typescript';
import fs from 'node:fs';
import path from 'node:path';
import {build} from 'esbuild';
const root = path.resolve('../..','vatsim-radar');
const specs = {
 atc:['app/composables/render/update/atc.ts'],
 vg:['app/composables/render/update/vatglasses.ts'],
 runway:['app/utils/shared/runway-detection.ts'],
 utils:['app/composables/render/update/utils.ts'],
 duplicates:['app/utils/server/vatsim/atc-duplicating.ts'],
 shared:['app/utils/shared/vatsim.ts',['facilitiesMap','getFacilityByCallsign','getTraconPrefixes','getTraconSuffix','getLongestTraconPrefix']],
 vatspy:['app/utils/server/vatsim/vatspy.ts',['parseDatFile','compileVatSpy']],
 vgcompile:['app/utils/server/vatglasses.ts',['combineJsonFiles','convertToDecimalDegrees','convertCoords']]
};
const printer=ts.createPrinter();
for(const [name,[file,keep]] of Object.entries(specs)){
 const source=fs.readFileSync(`vendor/${name}.ts`,'utf8');
 fs.writeFileSync(`vendor/${name}.ts`,source);
 const parsed=ts.createSourceFile(file,source,ts.ScriptTarget.Latest,true);
 let nodes=parsed.statements.filter(n=>!ts.isImportDeclaration(n));
 if(keep) nodes=nodes.filter(n=>keep.includes(n.name?.text)||n.declarationList?.declarations.some(d=>keep.includes(d.name.text)));
 let code=nodes.map(n=>printer.printNode(ts.EmitHint.Unspecified,n,parsed)).join('\n');
 if(name==='vgcompile')code+='\nexport { combineJsonFiles, convertCoords };';
 fs.writeFileSync(`vendor/${name}.js.txt`,ts.transpileModule(code,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText);
}
// Committed vendor sources and license keep builds independent of sibling checkouts.
await build({entryPoints:['worker.mjs'],bundle:true,platform:'node',format:'cjs',outfile:'dist/coverage.cjs',loader:{'.txt':'text'}});
await build({entryPoints:['coverage.mjs'],bundle:true,platform:'node',format:'esm',outfile:'dist/library.mjs',loader:{'.txt':'text'}});
