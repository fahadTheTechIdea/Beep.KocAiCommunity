# Workflow Node Catalog

O&G-specific node catalog for `Beep.KocAiCommunity`. Each entry: id, category, display name, ports, parameters, supported ML tasks, and the plan phase that creates it.

## Categories

- `source` — read data from datasets, projects, or connectors
- `transform` — preprocess, featurize, clean, balance
- `split` — train/validation/test split with safe defaults
- `trainer` — ML.NET trainers and AutoML
- `evaluator` — task-specific metrics
- `output` — save model, save dataset version, register artifact
- `control` — if/branch, loop, sub-workflow
- `industry` — O&G domain nodes (Phase 07a integrations)

## Source nodes

### `dataset.read`

- Category: `source`
- Inputs: none
- Outputs: `dataset` (typed `IDataView`)
- Parameters: `datasetId`, `datasetVersionId`, `rowLimit?`, `sampleStrategy?` (`head|random|stratified`)
- Plan phase: 07
- ML tasks: any

### `connector.ppdm.read`

- Category: `source` / `industry`
- Inputs: none
- Outputs: `dataset` (typed `IDataView`)
- Parameters: `connectorInstanceId`, `entityType` (well, wellbore, log, production), `filter?`
- Plan phase: 07a
- ML tasks: any

### `connector.openwells.read`

- Category: `source` / `industry`
- Inputs: none
- Outputs: `dataset` (typed `IDataView`)
- Parameters: `connectorInstanceId`, `entityType`, `filter?`
- Plan phase: 07a
- ML tasks: any

### `connector.ecosys.read`

- Category: `source` / `industry`
- Inputs: none
- Outputs: `dataset` (typed `IDataView`)
- Parameters: `connectorInstanceId`, `projectId?`, `filter?`
- Plan phase: 07a
- ML tasks: any

### `connector.sap.read`

- Category: `source` / `industry`
- Inputs: none
- Outputs: `dataset` (typed `IDataView`)
- Parameters: `connectorInstanceId`, `module` (`PM`, `MM`), `query`
- Plan phase: 07a
- ML tasks: any

### `connector.pi.read`

- Category: `source` / `industry`
- Inputs: none
- Outputs: `dataset` (typed `IDataView`)
- Parameters: `connectorInstanceId`, `afDatabase`, `tagPattern`, `fromUtc`, `toUtc`
- Plan phase: 07a
- ML tasks: any

### `connector.adls.read`

- Category: `source` / `industry`
- Inputs: none
- Outputs: `dataset` (typed `IDataView`)
- Parameters: `connectorInstanceId`, `path`, `format?` (`csv|parquet|delta`)
- Plan phase: 07a
- ML tasks: any

## Transform nodes

### `transform.normalize`

- Category: `transform`
- Inputs: `dataset`
- Outputs: `dataset`
- Parameters: `columns`, `method` (`minmax|zscore|robust`)
- Plan phase: 08
- ML tasks: any

### `transform.encode.categorical`

- Category: `transform`
- Inputs: `dataset`
- Outputs: `dataset`
- Parameters: `columns`, `method` (`onehot|hash|target`)
- Plan phase: 08
- ML tasks: any

### `transform.handle.missing`

- Category: `transform`
- Inputs: `dataset`
- Outputs: `dataset`
- Parameters: `columns`, `strategy` (`mean|median|mode|constant|drop`), `constantValue?`
- Plan phase: 08
- ML tasks: any

### `transform.balance`

- Category: `transform`
- Inputs: `dataset`
- Outputs: `dataset`
- Parameters: `labelColumn`, `strategy` (`oversample|undersample|smote`)
- Plan phase: 08
- ML tasks: binary classification, multiclass classification

### `transform.time.window`

- Category: `transform`
- Inputs: `dataset`
- Outputs: `dataset`
- Parameters: `timeColumn`, `windowSize`, `aggregation` (`mean|sum|min|max|count`)
- Plan phase: 08
- ML tasks: forecasting, time-series anomaly detection

## Split nodes

### `split.random`

- Category: `split`
- Inputs: `dataset`
- Outputs: `train`, `validation`, `test`
- Parameters: `trainFraction`, `validationFraction`, `seed`, `stratifyColumn?`
- Plan phase: 08
- ML tasks: classification, regression

### `split.temporal`

- Category: `split`
- Inputs: `dataset`
- Outputs: `train`, `validation`, `test`
- Parameters: `timeColumn`, `trainFraction`, `validationFraction`
- Plan phase: 08
- ML tasks: forecasting, time-series anomaly detection

## Trainer nodes

### `trainer.binary.fasttree`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `labelColumn`, `featureColumns`, `iterations?`, `learningRate?`, `numberOfLeaves?`, `minimumExampleCountPerLeaf?`
- Plan phase: 08
- ML tasks: binary classification

### `trainer.binary.sdca`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `labelColumn`, `featureColumns`, `l1Regularization?`, `l2Regularization?`
- Plan phase: 08
- ML tasks: binary classification

### `trainer.binary.automl`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`, `trials`
- Parameters: `labelColumn`, `featureColumns`, `trainingTimeSeconds`, `optimizationMetric?` (`accuracy|f1|auc|auPr`), `excludeTrainers?`
- Plan phase: 08
- ML tasks: binary classification

### `trainer.multiclass.fasttree`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `labelColumn`, `featureColumns`, `iterations?`, `learningRate?`, `numberOfLeaves?`
- Plan phase: 08
- ML tasks: multiclass classification

### `trainer.multiclass.sdca`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `labelColumn`, `featureColumns`, `l1Regularization?`, `l2Regularization?`
- Plan phase: 08
- ML tasks: multiclass classification

### `trainer.multiclass.automl`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`, `trials`
- Parameters: `labelColumn`, `featureColumns`, `trainingTimeSeconds`, `optimizationMetric?` (`microAccuracy|macroAccuracy|logLoss`), `excludeTrainers?`
- Plan phase: 08
- ML tasks: multiclass classification

### `trainer.regression.fasttree`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `labelColumn`, `featureColumns`, `iterations?`, `learningRate?`, `numberOfLeaves?`
- Plan phase: 08
- ML tasks: regression

### `trainer.regression.sdca`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `labelColumn`, `featureColumns`, `l1Regularization?`, `l2Regularization?`
- Plan phase: 08
- ML tasks: regression

### `trainer.regression.automl`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`, `trials`
- Parameters: `labelColumn`, `featureColumns`, `trainingTimeSeconds`, `optimizationMetric?` (`rSquared|rmse|mae`), `excludeTrainers?`
- Plan phase: 08
- ML tasks: regression

### `trainer.forecast.fastforest`

- Category: `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `timeColumn`, `valueColumn`, `horizon`, `seasonality?`, `windowSize?`
- Plan phase: 08
- ML tasks: forecasting

### `trainer.anomaly.isolationforest`

- Category: `trainer`
- Inputs: `train`
- Outputs: `model`
- Parameters: `featureColumns`, `contamination?`, `trees?`, `seed?`
- Plan phase: 08
- ML tasks: anomaly detection

### `trainer.recommendation.fieldaware`

- Category: `trainer`
- Inputs: `train`
- Outputs: `model`
- Parameters: `userColumn`, `itemColumn`, `ratingColumn`
- Plan phase: 08 (initial implementation)
- ML tasks: recommendation

## Evaluator nodes

### `evaluate.binary`

- Category: `evaluator`
- Inputs: `model`, `test`
- Outputs: `metrics`
- Parameters: `labelColumn`
- Metrics: accuracy, AUC, AUPRC, F1, precision, recall, log-loss, confusion matrix
- Plan phase: 08

### `evaluate.multiclass`

- Category: `evaluator`
- Inputs: `model`, `test`
- Outputs: `metrics`
- Parameters: `labelColumn`
- Metrics: micro-accuracy, macro-accuracy, per-class precision/recall/F1, confusion matrix

### `evaluate.regression`

- Category: `evaluator`
- Inputs: `model`, `test`
- Outputs: `metrics`
- Parameters: `labelColumn`
- Metrics: RSquared, RMSE, MAE, loss functions

### `evaluate.forecast`

- Category: `evaluator`
- Inputs: `model`, `test`
- Outputs: `metrics`
- Parameters: `timeColumn`, `valueColumn`
- Metrics: MAPE, RMSE, MAE

### `evaluate.recommendation`

- Category: `evaluator`
- Inputs: `model`, `test`
- Outputs: `metrics`
- Parameters: `userColumn`, `itemColumn`, `ratingColumn`
- Metrics: NDCG@k, MAP, precision, recall

### `evaluate.permutation`

- Category: `evaluator`
- Inputs: `model`, `test`
- Outputs: `featureImportance`
- Parameters: `labelColumn`, `permutationCount?`
- Plan phase: 08

## Output nodes

### `output.model.save`

- Category: `output`
- Inputs: `model`
- Outputs: `modelArtifact`
- Parameters: `name`, `semVer`, `modelType`, `notes?`
- Plan phase: 08/12

### `output.dataset.write`

- Category: `output`
- Inputs: `dataset`
- Outputs: `datasetVersion`
- Parameters: `name`, `datasetId?`, `notes?`
- Plan phase: 07/08

### `output.run.report`

- Category: `output`
- Inputs: `metrics`
- Outputs: `experimentArtifact`
- Parameters: `name`, `format` (`html|json|pdf`)
- Plan phase: 11

## Control nodes

### `control.if`

- Category: `control`
- Inputs: `dataset`, `condition`
- Outputs: `trueBranch`, `falseBranch`
- Parameters: `expression`
- Plan phase: 09

### `control.subworkflow`

- Category: `control`
- Inputs: none (workflow reference)
- Outputs: depends on sub-workflow
- Parameters: `workflowId`, `versionNumber`
- Plan phase: 09

## Industry (O&G) nodes

### `og.production.declinecurve`

- Category: `industry` / `transform`
- Inputs: `dataset`
- Outputs: `dataset` (with fitted decline parameters)
- Parameters: `timeColumn`, `rateColumn`, `modelType` (`exponential|hyperbolic|harmonic`)
- Plan phase: 08

### `og.reservoir.materialbalance`

- Category: `industry` / `transform`
- Inputs: `dataset`
- Outputs: `dataset`
- Parameters: `pressureColumn`, `productionColumn`, `pvtModel`
- Plan phase: 08

### `og.hse.incident.classify`

- Category: `industry` / `trainer`
- Inputs: `train`, `validation`
- Outputs: `model`
- Parameters: `descriptionColumn`, `labelColumn`
- Wraps binary classification trainer with text featurization

### `og.facility.anomaly`

- Category: `industry` / `trainer`
- Inputs: `train`
- Outputs: `model`
- Parameters: `featureColumns`, `sensorTagPattern`
- Wraps isolation forest

## Ports

Ports are typed. Supported types:

- `dataset` (`IDataView`)
- `model` (`ITransformer`)
- `metrics` (`RunMetric[]`)
- `trials` (`AutoMLTrial[]`)
- `modelArtifact` (`ArtifactReference`)
- `datasetVersion` (`DatasetVersion`)

Connect validation rejects type-mismatched edges at compile time and at runtime.

## Node registry

Nodes are registered by `INodeContributor` implementations in `Beep.KocAiCommunity.ML`. The registry is built at startup. JSON serialization uses the registered node id as the discriminator.

## Anti-patterns

- No user-uploaded scripts execute inside the Worker.
- No arbitrary code nodes. "Python script" node is explicitly out of scope.
- Connector outputs are read-only by default.
