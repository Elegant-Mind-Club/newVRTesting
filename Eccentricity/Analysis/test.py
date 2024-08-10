import pandas as pd
import seaborn as sns
import matplotlib.pyplot as plt

# Sample data
data1 = {
    "IndependentVar": [-30, -15, 0, 15, 30],
    "Mean": [475.75, 410.15, 428.33, 359.82, 396.25]
}

data2 = {
    "IndependentVar": [-30, -20, -10, 0, 10, 20, 30],
    "Mean": [494.50, 463.29, 429.29, 433.56, 370.50, 421.83, 505.38]
}

# Create DataFrames
df1 = pd.DataFrame(data1)
df2 = pd.DataFrame(data2)

# Combine DataFrames
combined_df = pd.concat([df1, df2], ignore_index=True)

# Ensure the data types are correct
combined_df['IndependentVar'] = combined_df['IndependentVar'].astype(float)
combined_df['Mean'] = combined_df['Mean'].astype(float)

# Split data into two segments
segment1 = combined_df[combined_df['IndependentVar'] <= 0]
segment2 = combined_df[combined_df['IndependentVar'] > 0]

# Plotting
plt.figure(figsize=(10, 6))

# Scatter plot
sns.scatterplot(data=combined_df, x="IndependentVar", y="Mean", color='blue')

# Line of best fit for segment 1
sns.regplot(data=segment1, x="IndependentVar", y="Mean", scatter=False, color='red', label='Fit: <= 0')

# Line of best fit for segment 2
sns.regplot(data=segment2, x="IndependentVar", y="Mean", scatter=False, color='green', label='Fit: > 0')

plt.xlabel('Independent Variable')
plt.ylabel('Mean Reaction Time')
plt.title('Mean Reaction Time vs Independent Variable')
plt.legend()
plt.show()
