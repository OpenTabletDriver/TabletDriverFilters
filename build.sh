
framework="net5"
output="./bin"
rm -rvf $output

for project in */*.csproj; do 
    dotnet build $project -f $framework -o $output
done